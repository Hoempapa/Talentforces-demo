using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace JobSync.Api.JobBoards.MockJobBoard;

/// <summary>
/// Haalt een access token op via OAuth2 client credentials en bewaart het tot net voor het verloopt.
/// Singleton: één token voor de hele applicatie.
/// </summary>
public class MockJobBoardTokenProvider(IHttpClientFactory httpFactory, IOptions<MockJobBoardOptions> options, ILogger<MockJobBoardTokenProvider> logger)
{
    public const string HttpClientNaam = "mockjobboard-token";

    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _token;
    private DateTimeOffset _vervalt;

    public async Task<string> GeefTokenAsync(CancellationToken ct)
    {
        if (_token is not null && DateTimeOffset.UtcNow < _vervalt) return _token;

        await _lock.WaitAsync(ct);
        try
        {
            if (_token is not null && DateTimeOffset.UtcNow < _vervalt) return _token;

            var o = options.Value;
            if (string.IsNullOrWhiteSpace(o.ClientSecret))
                throw new JobBoardException("MockJobBoard:ClientSecret is niet ingesteld (zie README, user-secrets).");

            var http = httpFactory.CreateClient(HttpClientNaam);
            using var response = await http.PostAsync("oauth/token", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = o.ClientId,
                ["client_secret"] = o.ClientSecret,
            }), ct);

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest)
                throw new JobBoardException($"Aanmelden bij het platform mislukt ({(int)response.StatusCode}). Controleer ClientId/ClientSecret.");
            response.EnsureSuccessStatusCode();

            var token = await response.Content.ReadFromJsonAsync<MockTokenResponse>(ct)
                        ?? throw new InvalidOperationException("Leeg token-antwoord.");
            _token = token.access_token;
            _vervalt = DateTimeOffset.UtcNow.AddSeconds(token.expires_in - 60); // marge tegen klokverschil
            logger.LogInformation("Nieuw access token opgehaald, geldig tot {Vervalt:HH:mm:ss}", _vervalt);
            return _token;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Vergeet() => _token = null;
}

/// <summary>
/// Zet het Bearer-token op elke request. Bij een 401 (bv. token ingetrokken of mock herstart)
/// wordt één keer een nieuw token gehaald en de request opnieuw verstuurd.
/// </summary>
public class MockJobBoardAuthHandler(MockJobBoardTokenProvider tokens) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokens.GeefTokenAsync(ct));
        var response = await base.SendAsync(request, ct);
        if (response.StatusCode != HttpStatusCode.Unauthorized) return response;

        response.Dispose();
        tokens.Vergeet();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokens.GeefTokenAsync(ct));
        return await base.SendAsync(request, ct);
    }
}
