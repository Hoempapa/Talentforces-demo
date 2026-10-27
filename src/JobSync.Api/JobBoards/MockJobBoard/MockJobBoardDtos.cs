namespace JobSync.Api.JobBoards.MockJobBoard;

// Het (fictieve) formaat van MockJobBoard. Bij een echte VDAB-client komen hier de VDAB-DTO's.

public record MockWerkplaats(string? Gemeente, string? Postcode);

public record MockVacatureRequest(
    string Referentie,
    string? Functietitel,
    string? Beschrijving,
    MockWerkplaats Werkplaats,
    string? Contract);

public record MockVacatureResponse(string Id, string Status);

public record MockTokenResponse(string access_token, string token_type, int expires_in);
