using System.Text.Json.Serialization;
using MockJobBoard.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<VacatureStore>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();
var log = app.Logger;

// ---------------------------------------------------------------------------
// OAuth2 client credentials (gesimuleerd)
// ---------------------------------------------------------------------------
app.MapPost("/oauth/token", async (HttpRequest request, TokenService tokens) =>
{
    if (!request.HasFormContentType)
        return Results.BadRequest(new { error = "invalid_request", error_description = "Verwacht application/x-www-form-urlencoded." });

    var form = await request.ReadFormAsync();
    if (form["grant_type"] != "client_credentials")
        return Results.BadRequest(new { error = "unsupported_grant_type" });
    if (!tokens.ControleerClient(form["client_id"], form["client_secret"]))
        return Results.Json(new { error = "invalid_client" }, statusCode: StatusCodes.Status401Unauthorized);

    return Results.Ok(tokens.GeefToken());
});

// ---------------------------------------------------------------------------
// Vacature-endpoints (beveiligd met Bearer-token, met af en toe een 503)
// ---------------------------------------------------------------------------
var vacatures = app.MapGroup("/vacatures")
    .AddEndpointFilter(async (ctx, next) =>
    {
        var http = ctx.HttpContext;
        if (!http.RequestServices.GetRequiredService<TokenService>().IsGeldig(http.Request.Headers.Authorization))
            return Results.Problem(title: "Ongeldig of verlopen token.", statusCode: StatusCodes.Status401Unauthorized);

        // Chaos: simuleer tijdelijke onbeschikbaarheid zodat retries zichtbaar worden.
        var kans = app.Configuration.GetValue("Chaos:FailureRate", 0.0);
        if (Random.Shared.NextDouble() < kans)
        {
            log.LogWarning("Chaos: 503 teruggegeven voor {Method} {Path}", http.Request.Method, http.Request.Path);
            http.Response.Headers.RetryAfter = "1";
            return Results.Problem(title: "Dienst tijdelijk niet beschikbaar.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        return await next(ctx);
    });

vacatures.MapGet("/", (VacatureStore store) => store.Alle());

vacatures.MapGet("/{id}", (string id, VacatureStore store) =>
    store.Zoek(id) is { } v ? Results.Ok(v) : Results.NotFound());

vacatures.MapPost("/", (VacatureRequest body, VacatureStore store) =>
{
    var fouten = VacatureValidator.Valideer(body);
    if (fouten.Count > 0) return Results.ValidationProblem(fouten, title: "Validatie mislukt.");

    // Idempotentie: dezelfde referentie twee keer publiceren levert geen dubbele vacature op.
    if (store.ZoekOpenOpReferentie(body.Referentie!) is { } bestaand)
        return Results.Problem(
            title: "Er staat al een open vacature online met deze referentie.",
            statusCode: StatusCodes.Status409Conflict,
            extensions: new Dictionary<string, object?> { ["bestaandId"] = bestaand.Id });

    var v = store.Maak(body);
    log.LogInformation("Vacature {Id} gepubliceerd ({Titel})", v.Id, v.Functietitel);
    return Results.Created($"/vacatures/{v.Id}", v);
});

vacatures.MapPut("/{id}", (string id, VacatureRequest body, VacatureStore store) =>
{
    if (store.Zoek(id) is not { } bestaand) return Results.NotFound();
    if (bestaand.Status == VacatureStatus.Afgesloten)
        return Results.Problem(title: "Een afgesloten vacature kan niet meer aangepast worden.", statusCode: StatusCodes.Status409Conflict);

    var fouten = VacatureValidator.Valideer(body with { Referentie = bestaand.Referentie });
    if (fouten.Count > 0) return Results.ValidationProblem(fouten, title: "Validatie mislukt.");

    var v = store.Wijzig(bestaand, body);
    log.LogInformation("Vacature {Id} aangepast", v.Id);
    return Results.Ok(v);
});

vacatures.MapPost("/{id}/afsluiten", (string id, VacatureStore store) =>
{
    if (store.Zoek(id) is not { } bestaand) return Results.NotFound();
    if (bestaand.Status == VacatureStatus.Afgesloten) return Results.Ok(bestaand); // idempotent

    var v = store.SluitAf(bestaand);
    log.LogInformation("Vacature {Id} afgesloten", v.Id);
    return Results.Ok(v);
});

app.MapGet("/", () => "MockJobBoard draait. Fictief vacatureplatform voor de JobSync-demo.");

app.Run();
