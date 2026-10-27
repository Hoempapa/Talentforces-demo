using System.Collections.Concurrent;

namespace MockJobBoard.Api;

/// <summary>In-memory opslag. Bij herstart van de mock is alles weg; voor een demo is dat prima.</summary>
public class VacatureStore
{
    private readonly ConcurrentDictionary<string, Vacature> _vacatures = new();
    private int _teller = 1000;

    public IEnumerable<Vacature> Alle() => _vacatures.Values.OrderBy(v => v.Id);

    public Vacature? Zoek(string id) => _vacatures.GetValueOrDefault(id);

    public Vacature? ZoekOpenOpReferentie(string referentie) =>
        _vacatures.Values.FirstOrDefault(v => v.Referentie == referentie && v.Status == VacatureStatus.Online);

    public Vacature Maak(VacatureRequest r)
    {
        var nu = DateTimeOffset.UtcNow;
        var id = $"MJB-{Interlocked.Increment(ref _teller)}";
        var v = new Vacature(id, r.Referentie!, r.Functietitel!, r.Beschrijving!, r.Werkplaats!, r.Contract!,
            VacatureStatus.Online, nu, nu, null);
        _vacatures[id] = v;
        return v;
    }

    public Vacature Wijzig(Vacature bestaand, VacatureRequest r)
    {
        var v = bestaand with
        {
            Functietitel = r.Functietitel!,
            Beschrijving = r.Beschrijving!,
            Werkplaats = r.Werkplaats!,
            Contract = r.Contract!,
            LaatstGewijzigdOp = DateTimeOffset.UtcNow
        };
        _vacatures[v.Id] = v;
        return v;
    }

    public Vacature SluitAf(Vacature bestaand)
    {
        var nu = DateTimeOffset.UtcNow;
        var v = bestaand with { Status = VacatureStatus.Afgesloten, AfgeslotenOp = nu, LaatstGewijzigdOp = nu };
        _vacatures[v.Id] = v;
        return v;
    }
}
