using System.Net.Http.Headers;
using JobSync.Api.Domain;
using Microsoft.Extensions.Options;

namespace JobSync.Api.Ats;

/// <summary>
/// Leest en schrijft de tabel "Vacatures" via de Airtable REST API.
/// Rate limiting (5 requests/seconde per base) en retries op 429/5xx zitten in de
/// HttpClient-pipeline (zie Program.cs), niet in deze klasse.
/// </summary>
public class AirtableClient(HttpClient http, IOptions<AirtableOptions> options, ILogger<AirtableClient> logger) : IAtsRepository
{
    // Airtable laat maximaal 10 records per create/update-request toe.
    private const int MaxRecordsPerUpdate = 10;

    private static readonly string[] Velden =
        ["Titel", "Omschrijving", "Locatie", "Contracttype", "Status", "ExternId", "LaatsteSync", "GewijzigdOp", "SyncFout"];

    private readonly AirtableOptions _opties = options.Value;

    private string TabelUrl => $"{_opties.BaseId}/{Uri.EscapeDataString(_opties.Tabel)}";

    public async Task<IReadOnlyList<Vacature>> HaalVacaturesOpAsync(CancellationToken ct = default)
    {
        var resultaat = new List<Vacature>();
        string? offset = null;
        var veldParams = string.Join("&", Velden.Select(v => $"fields%5B%5D={Uri.EscapeDataString(v)}"));

        // Airtable pagineert per 100 records; "offset" wijst naar de volgende pagina.
        do
        {
            var url = $"{TabelUrl}?pageSize=100&{veldParams}" + (offset is null ? "" : $"&offset={Uri.EscapeDataString(offset)}");
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            Authenticeer(request);

            using var response = await http.SendAsync(request, ct);
            await ControleerAsync(response, "ophalen vacatures", ct);

            var pagina = await response.Content.ReadFromJsonAsync<AirtableLijst<VacatureVelden>>(ct)
                         ?? throw new InvalidOperationException("Leeg antwoord van Airtable.");
            resultaat.AddRange(pagina.Records.Select(NaarDomein));
            offset = pagina.Offset;
        } while (offset is not null);

        logger.LogInformation("{Aantal} vacatures opgehaald uit Airtable", resultaat.Count);
        return resultaat;
    }

    public async Task SchrijfTerugAsync(IReadOnlyCollection<SyncTerugkoppeling> terugkoppelingen, CancellationToken ct = default)
    {
        foreach (var batch in terugkoppelingen.Chunk(MaxRecordsPerUpdate))
        {
            var body = new AirtableUpdate(batch.Select(NaarUpdateRecord).ToList());
            using var request = new HttpRequestMessage(HttpMethod.Patch, TabelUrl) { Content = JsonContent.Create(body) };
            Authenticeer(request);

            using var response = await http.SendAsync(request, ct);
            await ControleerAsync(response, "terugschrijven sync-resultaat", ct);
            logger.LogInformation("Sync-resultaat teruggeschreven naar Airtable voor {Aantal} records", batch.Length);
        }
    }

    internal static Vacature NaarDomein(AirtableRecord<VacatureVelden> r) => new(
        r.Id,
        r.Fields.Titel,
        r.Fields.Omschrijving,
        r.Fields.Locatie,
        r.Fields.Contracttype,
        Enum.TryParse<VacatureStatus>(r.Fields.Status, ignoreCase: true, out var s) ? s : VacatureStatus.Onbekend,
        string.IsNullOrWhiteSpace(r.Fields.ExternId) ? null : r.Fields.ExternId.Trim(),
        r.Fields.LaatsteSync,
        r.Fields.GewijzigdOp,
        r.Fields.SyncFout);

    internal static AirtableUpdateRecord NaarUpdateRecord(SyncTerugkoppeling t)
    {
        // SyncFout altijd meesturen: null wist een oude fout na een geslaagde sync.
        var velden = new Dictionary<string, object?> { ["SyncFout"] = t.SyncFout };
        if (t.ExternId is not null) velden["ExternId"] = t.ExternId;
        if (t.LaatsteSync is not null) velden["LaatsteSync"] = t.LaatsteSync.Value.UtcDateTime.ToString("O");
        return new AirtableUpdateRecord(t.RecordId, velden);
    }

    private void Authenticeer(HttpRequestMessage request) =>
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _opties.Token);

    private static async Task ControleerAsync(HttpResponseMessage response, string actie, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync(ct);
        throw new HttpRequestException($"Airtable-fout bij {actie}: {(int)response.StatusCode} {body}", null, response.StatusCode);
    }
}
