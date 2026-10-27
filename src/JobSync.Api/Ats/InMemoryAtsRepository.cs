using System.Collections.Concurrent;
using JobSync.Api.Domain;

namespace JobSync.Api.Ats;

/// <summary>
/// Vervanger voor Airtable wanneer er (nog) geen token/base-id ingesteld is.
/// Zo kan de demo ook offline draaien. Wordt enkel gebruikt als Airtable niet geconfigureerd is.
/// </summary>
public class InMemoryAtsRepository : IAtsRepository
{
    private readonly ConcurrentDictionary<string, Vacature> _data = new();

    public InMemoryAtsRepository()
    {
        var nu = DateTimeOffset.UtcNow;
        Voeg(new("recDemo1", "Backend developer .NET", "Je bouwt integraties tussen ons ATS en externe vacatureplatformen.", "9000 Gent", "Vast", VacatureStatus.Gepubliceerd, null, null, nu, null));
        Voeg(new("recDemo2", "Recruiter IT", "Je zoekt en begeleidt IT-profielen voor onze klanten in heel Vlaanderen.", "Antwerpen", "Tijdelijk", VacatureStatus.Concept, null, null, nu, null));
        Voeg(new("recDemo3", "Magazijnmedewerker met een veel te lange functietitel die niet door de validatie raakt", "Je staat in voor orderpicking en het klaarmaken van leveringen.", "2800 Mechelen", "Interim", VacatureStatus.Gepubliceerd, null, null, nu, null));
        Voeg(new("recDemo4", "Boekhouder", "Je volgt de boekhouding op van een portefeuille kmo-klanten.", "Leuven", "Vast", VacatureStatus.Afgesloten, null, null, nu, null));
    }

    private void Voeg(Vacature v) => _data[v.RecordId] = v;

    public Task<IReadOnlyList<Vacature>> HaalVacaturesOpAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Vacature>>(_data.Values.OrderBy(v => v.RecordId).ToList());

    public Task SchrijfTerugAsync(IReadOnlyCollection<SyncTerugkoppeling> terugkoppelingen, CancellationToken ct = default)
    {
        foreach (var t in terugkoppelingen)
            _data.AddOrUpdate(t.RecordId, _ => throw new KeyNotFoundException(t.RecordId), (_, v) => v with
            {
                ExternId = t.ExternId ?? v.ExternId,
                LaatsteSync = t.LaatsteSync ?? v.LaatsteSync,
                SyncFout = t.SyncFout
            });
        return Task.CompletedTask;
    }
}
