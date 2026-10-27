using Microsoft.Extensions.Options;

namespace JobSync.Api.Sync;

public class SyncOptions
{
    public const string Sectie = "Sync";

    public bool Automatisch { get; set; } = true;
    public int IntervalSeconden { get; set; } = 300;
}

/// <summary>Start periodiek een sync. De handmatige knop op het dashboard gebruikt dezelfde SyncService.</summary>
public class SyncBackgroundService(SyncService sync, IOptions<SyncOptions> options, SyncLog log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var o = options.Value;
        if (!o.Automatisch)
        {
            log.Info("Automatische sync staat uit; enkel handmatig via 'Sync nu'.");
            return;
        }

        log.Info($"Automatische sync elke {o.IntervalSeconden} seconden.");
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(o.IntervalSeconden));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await sync.SynchroniseerAsync("automatisch", stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Een mislukte ronde mag de achtergrondtaak nooit stoppen.
                log.Fout($"Onverwachte fout in automatische sync: {ex.Message}");
            }
        }
    }
}
