namespace JobSync.Api.JobBoards.MockJobBoard;

public class MockJobBoardOptions
{
    public const string Sectie = "MockJobBoard";

    public string BaseUrl { get; set; } = "http://localhost:5200/";
    public string ClientId { get; set; } = "";

    /// <summary>Via user-secrets, net zoals bij een echt platform.</summary>
    public string? ClientSecret { get; set; }
}
