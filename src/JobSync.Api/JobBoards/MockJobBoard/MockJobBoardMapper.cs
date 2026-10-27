using System.Text.RegularExpressions;
using JobSync.Api.Domain;

namespace JobSync.Api.JobBoards.MockJobBoard;

/// <summary>
/// Vertaalt een Airtable-vacature naar het formaat van MockJobBoard.
/// Pure functie zonder I/O, zodat ze eenvoudig te unit-testen is.
/// </summary>
public static partial class MockJobBoardMapper
{
    // Keuzes in het Airtable-veld "Contracttype" → codes van het platform.
    private static readonly Dictionary<string, string> Contracten = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Vast"] = "VAST",
        ["Tijdelijk"] = "TIJDELIJK",
        ["Interim"] = "INTERIM",
        ["Freelance"] = "FREELANCE",
        ["Studentenjob"] = "STUDENT",
    };

    public static MockVacatureRequest NaarRequest(Vacature v) => new(
        Referentie: v.RecordId,
        Functietitel: Opschonen(v.Titel),
        Beschrijving: Opschonen(v.Omschrijving),
        Werkplaats: NaarWerkplaats(v.Locatie),
        Contract: NaarContract(v.Contracttype));

    /// <summary>"9000 Gent" → postcode 9000, gemeente Gent. "Gent" → enkel gemeente.</summary>
    public static MockWerkplaats NaarWerkplaats(string? locatie)
    {
        var tekst = Opschonen(locatie);
        if (tekst is null) return new MockWerkplaats(null, null);

        var match = PostcodeEnGemeente().Match(tekst);
        return match.Success
            ? new MockWerkplaats(match.Groups["gemeente"].Value.Trim(), match.Groups["postcode"].Value)
            : new MockWerkplaats(tekst, null);
    }

    /// <summary>
    /// Leeg contracttype → null (het platform meldt dan zelf dat het verplicht is).
    /// Onbekend contracttype → fout: liever een duidelijke melding dan een gok.
    /// </summary>
    public static string? NaarContract(string? contracttype)
    {
        var tekst = Opschonen(contracttype);
        if (tekst is null) return null;
        return Contracten.TryGetValue(tekst, out var code)
            ? code
            : throw new JobBoardException($"Contracttype '{tekst}' kan niet vertaald worden. Gekend: {string.Join(", ", Contracten.Keys)}.");
    }

    private static string? Opschonen(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    [GeneratedRegex(@"^(?<postcode>\d{4})\s+(?<gemeente>.+)$")]
    private static partial Regex PostcodeEnGemeente();
}
