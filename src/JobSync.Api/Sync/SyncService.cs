using JobSync.Api.Domain;
using JobSync.Api.JobBoards;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace JobSync.Api.Sync;

public record SyncRapport(int Gepubliceerd, int Aangepast, int Afgesloten, int Fouten, int Ongewijzigd);

/// <summary>
/// Voert één sync-ronde uit: vacatures lezen uit het ATS, per vacature de juiste actie op het platform
/// uitvoeren en het resultaat terugschrijven. Singleton; er loopt nooit meer dan één sync tegelijk.
/// </summary>
public class SyncService(IServiceScopeFactory scopes, SyncLog log)
{
    private readonly SemaphoreSlim _bezig = new(1, 1);

    public bool IsBezig => _bezig.CurrentCount == 0;
    public DateTimeOffset? LaatsteRun { get; private set; }

    /// <returns>Het rapport, of null als er al een sync liep.</returns>
    public async Task<SyncRapport?> SynchroniseerAsync(string aanleiding, CancellationToken ct = default)
    {
        if (!await _bezig.WaitAsync(0, ct))
        {
            log.Waarschuwing($"Sync ({aanleiding}) overgeslagen: er loopt al een sync.");
            return null;
        }

        try
        {
            // Typed HttpClients zijn kortlevend; daarom per ronde een eigen scope.
            using var scope = scopes.CreateScope();
            var ats = scope.ServiceProvider.GetRequiredService<IAtsRepository>();
            var platform = scope.ServiceProvider.GetRequiredService<IJobBoardClient>();
            return await VoerUitAsync(ats, platform, aanleiding, ct);
        }
        finally
        {
            LaatsteRun = DateTimeOffset.Now;
            _bezig.Release();
        }
    }

    private async Task<SyncRapport> VoerUitAsync(IAtsRepository ats, IJobBoardClient platform, string aanleiding, CancellationToken ct)
    {
        // Tijdstip VÓÓR het lezen: wie tijdens de sync nog iets wijzigt in Airtable,
        // krijgt een GewijzigdOp dat later valt, en wordt bij de volgende ronde opnieuw opgepikt.
        var syncMoment = DateTimeOffset.UtcNow;
        log.Info($"Sync gestart ({aanleiding}) naar {platform.Naam}.");

        IReadOnlyList<Vacature> vacatures;
        try
        {
            vacatures = await ats.HaalVacaturesOpAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.Fout($"Vacatures ophalen uit het ATS mislukt: {ex.Message}");
            return new SyncRapport(0, 0, 0, 1, 0);
        }

        var terugkoppelingen = new List<SyncTerugkoppeling>();
        int gepubliceerd = 0, aangepast = 0, afgesloten = 0, fouten = 0, ongewijzigd = 0;

        foreach (var v in vacatures)
        {
            var actie = SyncBeslisser.Bepaal(v);
            if (actie == SyncActie.Geen) { ongewijzigd++; continue; }

            var label = $"'{v.Titel}' ({v.RecordId})";
            try
            {
                var externId = v.ExternId;
                switch (actie)
                {
                    case SyncActie.Publiceren:
                        externId = await platform.PubliceerAsync(v, ct);
                        gepubliceerd++;
                        log.Succes($"Gepubliceerd: {label} → {externId}");
                        break;
                    case SyncActie.Aanpassen:
                        await platform.WijzigAsync(v.ExternId!, v, ct);
                        aangepast++;
                        log.Succes($"Aangepast: {label} ({externId})");
                        break;
                    case SyncActie.Afsluiten:
                        await platform.SluitAfAsync(v.ExternId!, ct);
                        afgesloten++;
                        log.Succes($"Afgesloten: {label} ({externId})");
                        break;
                }
                terugkoppelingen.Add(new SyncTerugkoppeling(v.RecordId, externId, syncMoment, SyncFout: null));
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                // LaatsteSync blijft ongewijzigd: de vacature wordt bij de volgende ronde opnieuw geprobeerd.
                fouten++;
                var melding = Foutmelding(ex);
                log.Fout($"{actie} mislukt voor {label}: {melding}");
                terugkoppelingen.Add(new SyncTerugkoppeling(v.RecordId, null, null, $"{DateTimeOffset.Now:dd/MM HH:mm} {actie}: {melding}"));
            }
        }

        if (terugkoppelingen.Count > 0)
        {
            try
            {
                await ats.SchrijfTerugAsync(terugkoppelingen, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Niet erg voor publicaties: dankzij de referentie geeft het platform bij de volgende
                // ronde een 409 met het bestaande id, en nemen we dat over (geen dubbele vacature).
                log.Fout($"Terugschrijven naar het ATS mislukt: {ex.Message}");
            }
        }

        var rapport = new SyncRapport(gepubliceerd, aangepast, afgesloten, fouten, ongewijzigd);
        var samenvatting = $"Sync klaar: {gepubliceerd} gepubliceerd, {aangepast} aangepast, {afgesloten} afgesloten, {fouten} fouten, {ongewijzigd} ongewijzigd.";
        if (fouten > 0) log.Waarschuwing(samenvatting); else log.Info(samenvatting);
        return rapport;
    }

    /// <summary>Technische fouten omzetten naar een melding die een recruiter begrijpt.</summary>
    internal static string Foutmelding(Exception ex) => ex switch
    {
        JobBoardException => ex.Message,
        BrokenCircuitException => "Platform tijdelijk geblokkeerd na te veel fouten (circuit breaker). Volgende ronde opnieuw.",
        TimeoutRejectedException or OperationCanceledException => "Time-out: platform antwoordde niet op tijd. Volgende ronde opnieuw.",
        HttpRequestException => $"Platform onbereikbaar na herhaalde pogingen ({ex.Message}). Volgende ronde opnieuw.",
        _ => $"Onverwachte fout: {ex.Message}"
    };
}
