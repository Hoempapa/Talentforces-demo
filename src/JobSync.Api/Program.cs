using System.Net;
using System.Threading.RateLimiting;
using JobSync.Api.Ats;
using JobSync.Api.Components;
using JobSync.Api.Domain;
using JobSync.Api.JobBoards;
using JobSync.Api.JobBoards.MockJobBoard;
using JobSync.Api.Sync;
using Microsoft.Extensions.Http.Resilience;
using Polly;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ---------------------------------------------------------------------------
// ATS-bron: Airtable (of een in-memory demo-bron als er geen token is)
// ---------------------------------------------------------------------------
builder.Services.Configure<AirtableOptions>(builder.Configuration.GetSection(AirtableOptions.Sectie));
var airtable = builder.Configuration.GetSection(AirtableOptions.Sectie).Get<AirtableOptions>() ?? new();

if (airtable.IsGeconfigureerd)
{
    builder.Services.AddHttpClient<IAtsRepository, AirtableClient>(c => c.BaseAddress = new Uri(airtable.BaseUrl))
        .AddResilienceHandler("airtable", pipeline =>
        {
            // 1) Retry (buitenste laag). Airtable vraagt om 30 s te wachten na een 429.
            pipeline.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromSeconds(1),
                DelayGenerator = args => ValueTask.FromResult<TimeSpan?>(
                    args.Outcome.Result?.StatusCode == HttpStatusCode.TooManyRequests ? TimeSpan.FromSeconds(30) : null)
            });

            // 2) Client-side throttling: max 5 requests per seconde per base (de limiet van Airtable).
            //    Zo vermijden we 429's in plaats van ze achteraf op te vangen.
            pipeline.AddRateLimiter(new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromSeconds(1),
                SegmentsPerWindow = 5,
                QueueLimit = 100,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            }));

            // 3) Timeout per poging.
            pipeline.AddTimeout(TimeSpan.FromSeconds(15));
        });
}
else
{
    builder.Services.AddSingleton<IAtsRepository, InMemoryAtsRepository>();
}

// ---------------------------------------------------------------------------
// Vacatureplatform: MockJobBoard. Een echte VDAB-client registreer je hier in de plaats.
// ---------------------------------------------------------------------------
builder.Services.Configure<MockJobBoardOptions>(builder.Configuration.GetSection(MockJobBoardOptions.Sectie));
var mockJobBoard = builder.Configuration.GetSection(MockJobBoardOptions.Sectie).Get<MockJobBoardOptions>() ?? new();

builder.Services.AddHttpClient(MockJobBoardTokenProvider.HttpClientNaam, c => c.BaseAddress = new Uri(mockJobBoard.BaseUrl))
    .AddStandardResilienceHandler();
builder.Services.AddSingleton<MockJobBoardTokenProvider>();
builder.Services.AddTransient<MockJobBoardAuthHandler>();

var jobBoardHttp = builder.Services.AddHttpClient<IJobBoardClient, MockJobBoardClient>(c => c.BaseAddress = new Uri(mockJobBoard.BaseUrl));
jobBoardHttp
    // Buitenste laag: standaard resilience (retry met exponential backoff + jitter, respecteert Retry-After,
    // circuit breaker, time-outs). Retries op 5xx, 408, 429 en netwerkfouten.
    .AddStandardResilienceHandler()
    .Configure((o, sp) =>
    {
        var log = sp.GetRequiredService<SyncLog>();
        o.Retry.OnRetry = args =>
        {
            var reden = args.Outcome.Result is { } r ? $"HTTP {(int)r.StatusCode}" : args.Outcome.Exception?.GetType().Name;
            log.Waarschuwing($"Tijdelijke fout ({reden}), poging {args.AttemptNumber + 2} over {args.RetryDelay.TotalSeconds:0.#} s.");
            return ValueTask.CompletedTask;
        };
    });
// Binnenste laag: Bearer-token toevoegen (elke retry krijgt zo een geldig token).
jobBoardHttp.AddHttpMessageHandler<MockJobBoardAuthHandler>();

// ---------------------------------------------------------------------------
// Sync: handmatig (dashboard) en periodiek (BackgroundService)
// ---------------------------------------------------------------------------
builder.Services.Configure<SyncOptions>(builder.Configuration.GetSection(SyncOptions.Sectie));
builder.Services.AddSingleton<SyncLog>();
builder.Services.AddSingleton<SyncService>();
builder.Services.AddHostedService<SyncBackgroundService>();

var app = builder.Build();

if (!airtable.IsGeconfigureerd)
    app.Logger.LogWarning("Airtable is niet geconfigureerd (Airtable:Token / Airtable:BaseId). De in-memory demo-bron wordt gebruikt.");

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAntiforgery();

// Sync starten via HTTP, bv. vanuit een Zapier-webhook wanneer een record in Airtable wijzigt.
// Demo: geen authenticatie. In productie: API-key of HMAC-handtekening.
app.MapPost("/api/sync", async (SyncService sync, CancellationToken ct) =>
    await sync.SynchroniseerAsync("api", ct) is { } rapport
        ? Results.Ok(rapport)
        : Results.Conflict(new { melding = "Er loopt al een sync." }));

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
