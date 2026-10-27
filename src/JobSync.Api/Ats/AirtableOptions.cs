namespace JobSync.Api.Ats;

public class AirtableOptions
{
    public const string Sectie = "Airtable";

    public string BaseUrl { get; set; } = "https://api.airtable.com/v0/";
    public string Tabel { get; set; } = "Vacatures";

    /// <summary>Personal access token. Enkel via user-secrets of een environment variable, nooit in appsettings.json.</summary>
    public string? Token { get; set; }

    /// <summary>Base-id (begint met "app..."). Ook via user-secrets.</summary>
    public string? BaseId { get; set; }

    public bool IsGeconfigureerd => !string.IsNullOrWhiteSpace(Token) && !string.IsNullOrWhiteSpace(BaseId);
}
