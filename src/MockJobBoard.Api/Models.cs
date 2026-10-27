namespace MockJobBoard.Api;

// Fictief schema van "MockJobBoard". Dit is bewust NIET het VDAB-formaat:
// het dient enkel om een realistische vacature-API te simuleren.

public static class Contracttypes
{
    public static readonly string[] Toegelaten = ["VAST", "TIJDELIJK", "INTERIM", "FREELANCE", "STUDENT"];
}

public record Werkplaats(string? Gemeente, string? Postcode);

/// <summary>Body voor POST /vacatures en PUT /vacatures/{id}.</summary>
public record VacatureRequest(
    string? Referentie,      // id van de vacature in het bronsysteem (bv. Airtable record-id)
    string? Functietitel,
    string? Beschrijving,
    Werkplaats? Werkplaats,
    string? Contract);

public enum VacatureStatus { Online, Afgesloten }

public record Vacature(
    string Id,
    string Referentie,
    string Functietitel,
    string Beschrijving,
    Werkplaats Werkplaats,
    string Contract,
    VacatureStatus Status,
    DateTimeOffset GepubliceerdOp,
    DateTimeOffset LaatstGewijzigdOp,
    DateTimeOffset? AfgeslotenOp);

public record TokenResponse(string access_token, string token_type, int expires_in);
