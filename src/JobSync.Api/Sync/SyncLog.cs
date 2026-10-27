namespace JobSync.Api.Sync;

public enum LogNiveau { Info, Succes, Waarschuwing, Fout }

public record LogRegel(DateTimeOffset Tijd, LogNiveau Niveau, string Bericht);

/// <summary>
/// In-memory log van de laatste sync-acties, voor het dashboard. Singleton.
/// Schrijft tegelijk naar ILogger, zodat alles ook in de console/logging-infrastructuur staat.
/// </summary>
public class SyncLog(ILogger<SyncLog> logger)
{
    private const int MaxRegels = 200;
    private readonly LinkedList<LogRegel> _regels = new();
    private readonly Lock _lock = new();

    /// <summary>Wordt afgevuurd bij elke nieuwe regel, zodat het dashboard live kan bijwerken.</summary>
    public event Action? Gewijzigd;

    public IReadOnlyList<LogRegel> Regels
    {
        get { lock (_lock) return _regels.ToList(); }
    }

    public void Info(string bericht) => Voeg(LogNiveau.Info, bericht);
    public void Succes(string bericht) => Voeg(LogNiveau.Succes, bericht);
    public void Waarschuwing(string bericht) => Voeg(LogNiveau.Waarschuwing, bericht);
    public void Fout(string bericht) => Voeg(LogNiveau.Fout, bericht);

    private void Voeg(LogNiveau niveau, string bericht)
    {
        lock (_lock)
        {
            _regels.AddFirst(new LogRegel(DateTimeOffset.Now, niveau, bericht));
            if (_regels.Count > MaxRegels) _regels.RemoveLast();
        }

        var level = niveau switch
        {
            LogNiveau.Fout => LogLevel.Error,
            LogNiveau.Waarschuwing => LogLevel.Warning,
            _ => LogLevel.Information
        };
        logger.Log(level, "{Bericht}", bericht);
        Gewijzigd?.Invoke();
    }
}
