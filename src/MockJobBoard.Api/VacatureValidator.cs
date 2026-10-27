namespace MockJobBoard.Api;

public static class VacatureValidator
{
    public const int MaxTitelLengte = 80;
    public const int MinBeschrijvingLengte = 30;
    public const int MaxBeschrijvingLengte = 5000;

    /// <summary>Geeft een dictionary veld → fouten terug (leeg = geldig), in het formaat van ValidationProblemDetails.</summary>
    public static Dictionary<string, string[]> Valideer(VacatureRequest r)
    {
        var fouten = new Dictionary<string, List<string>>();
        void Fout(string veld, string melding)
        {
            if (!fouten.TryGetValue(veld, out var lijst)) fouten[veld] = lijst = [];
            lijst.Add(melding);
        }

        if (string.IsNullOrWhiteSpace(r.Referentie))
            Fout("referentie", "Referentie is verplicht.");

        if (string.IsNullOrWhiteSpace(r.Functietitel))
            Fout("functietitel", "Functietitel is verplicht.");
        else if (r.Functietitel.Length > MaxTitelLengte)
            Fout("functietitel", $"Functietitel mag maximaal {MaxTitelLengte} tekens bevatten (nu {r.Functietitel.Length}).");

        if (string.IsNullOrWhiteSpace(r.Beschrijving))
            Fout("beschrijving", "Beschrijving is verplicht.");
        else if (r.Beschrijving.Length < MinBeschrijvingLengte)
            Fout("beschrijving", $"Beschrijving moet minstens {MinBeschrijvingLengte} tekens bevatten.");
        else if (r.Beschrijving.Length > MaxBeschrijvingLengte)
            Fout("beschrijving", $"Beschrijving mag maximaal {MaxBeschrijvingLengte} tekens bevatten.");

        if (string.IsNullOrWhiteSpace(r.Werkplaats?.Gemeente))
            Fout("werkplaats.gemeente", "Gemeente van de werkplaats is verplicht.");

        if (string.IsNullOrWhiteSpace(r.Contract))
            Fout("contract", "Contracttype is verplicht.");
        else if (!Contracttypes.Toegelaten.Contains(r.Contract))
            Fout("contract", $"Onbekend contracttype '{r.Contract}'. Toegelaten: {string.Join(", ", Contracttypes.Toegelaten)}.");

        return fouten.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray());
    }
}
