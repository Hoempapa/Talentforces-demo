using System.Net;
using System.Text.Json;
using JobSync.Api.Domain;

namespace JobSync.Api.JobBoards.MockJobBoard;

/// <summary>
/// Implementatie van <see cref="IJobBoardClient"/> voor het fictieve MockJobBoard.
/// Authenticatie en retries zitten in de HttpClient-pipeline (MockJobBoardAuthHandler + resilience handler).
/// </summary>
public class MockJobBoardClient(HttpClient http, ILogger<MockJobBoardClient> logger) : IJobBoardClient
{
    public string Naam => "MockJobBoard";

    public async Task<string> PubliceerAsync(Vacature vacature, CancellationToken ct = default)
    {
        var body = MockJobBoardMapper.NaarRequest(vacature);
        using var response = await http.PostAsJsonAsync("vacatures", body, ct);

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            // Bestaat al (bv. vorige sync publiceerde wel, maar het terugschrijven naar Airtable mislukte).
            // We nemen het bestaande id over in plaats van een dubbele vacature te maken.
            var probleem = await LeesProbleemAsync(response, ct);
            if (probleem.TryGetProperty("bestaandId", out var id) && id.GetString() is { } bestaandId)
            {
                logger.LogWarning("Vacature {RecordId} stond al online als {ExternId}; id overgenomen", vacature.RecordId, bestaandId);
                return bestaandId;
            }
        }

        await ControleerAsync(response, ct);
        var resultaat = await response.Content.ReadFromJsonAsync<MockVacatureResponse>(ct)
                        ?? throw new JobBoardException("Leeg antwoord van het platform.");
        return resultaat.Id;
    }

    public async Task WijzigAsync(string externId, Vacature vacature, CancellationToken ct = default)
    {
        using var response = await http.PutAsJsonAsync($"vacatures/{Uri.EscapeDataString(externId)}", MockJobBoardMapper.NaarRequest(vacature), ct);
        await ControleerAsync(response, ct);
    }

    public async Task SluitAfAsync(string externId, CancellationToken ct = default)
    {
        using var response = await http.PostAsync($"vacatures/{Uri.EscapeDataString(externId)}/afsluiten", null, ct);
        await ControleerAsync(response, ct);
    }

    /// <summary>Vertaalt foutantwoorden naar een leesbare <see cref="JobBoardException"/>.</summary>
    private static async Task ControleerAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;

        var probleem = await LeesProbleemAsync(response, ct);
        var titel = probleem.TryGetProperty("title", out var t) ? t.GetString() : null;

        switch (response.StatusCode)
        {
            case HttpStatusCode.BadRequest when probleem.TryGetProperty("errors", out var errors):
                var details = errors.EnumerateObject()
                    .SelectMany(veld => veld.Value.EnumerateArray().Select(m => m.GetString()));
                throw new JobBoardException("Geweigerd door platform: " + string.Join(" ", details));
            case HttpStatusCode.NotFound:
                throw new JobBoardException("Vacature bestaat niet (meer) op het platform. Maak ExternId leeg om opnieuw te publiceren.");
            case HttpStatusCode.Conflict:
                throw new JobBoardException("Conflict: " + titel);
            default:
                // 5xx die na alle retries nog steeds faalt komt hier terecht.
                throw new HttpRequestException($"Platform antwoordde {(int)response.StatusCode}: {titel}", null, response.StatusCode);
        }
    }

    private static readonly JsonElement LeegObject = JsonDocument.Parse("{}").RootElement;

    /// <summary>Leest een ProblemDetails-body; geeft altijd een JSON-object terug, ook bij een lege of rare body.</summary>
    private static async Task<JsonElement> LeesProbleemAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            return json.ValueKind == JsonValueKind.Object ? json : LeegObject;
        }
        catch (JsonException)
        {
            return LeegObject;
        }
    }
}
