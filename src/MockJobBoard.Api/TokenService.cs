using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace MockJobBoard.Api;

/// <summary>Simuleert een OAuth2-server (client credentials flow): geeft tokens uit en controleert ze.</summary>
public class TokenService(IConfiguration config)
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _tokens = new();
    private int Geldigheid => config.GetValue("Auth:TokenLifetimeSeconds", 3600);

    public bool ControleerClient(string? clientId, string? clientSecret) =>
        clientId == config["Auth:ClientId"] && clientSecret == config["Auth:ClientSecret"];

    public TokenResponse GeefToken()
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        _tokens[token] = DateTimeOffset.UtcNow.AddSeconds(Geldigheid);
        return new TokenResponse(token, "Bearer", Geldigheid);
    }

    public bool IsGeldig(string? authorizationHeader)
    {
        if (authorizationHeader is null || !authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return false;
        var token = authorizationHeader["Bearer ".Length..].Trim();
        return _tokens.TryGetValue(token, out var vervalt) && vervalt > DateTimeOffset.UtcNow;
    }
}
