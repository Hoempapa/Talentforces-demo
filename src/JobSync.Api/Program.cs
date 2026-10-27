using System.Net;
using System.Threading.RateLimiting;
using JobSync.Api.Ats;
using JobSync.Api.Components;
using JobSync.Api.Domain;
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

var app = builder.Build();

if (!airtable.IsGeconfigureerd)
    app.Logger.LogWarning("Airtable is niet geconfigureerd (Airtable:Token / Airtable:BaseId). De in-memory demo-bron wordt gebruikt.");

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
