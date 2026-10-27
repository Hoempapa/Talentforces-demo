namespace JobSync.Api.Ats;

/// <summary>
/// Telt de echte HTTP-calls naar Airtable (inclusief retries) sinds de app opstartte.
/// Het gratis Airtable-plan laat maar 1.000 API-calls per maand toe, dus dit is zichtbaar op het dashboard.
/// </summary>
public class AirtableVerbruik
{
    private int _aantal;
    public int Aantal => _aantal;

    public void Tel() => Interlocked.Increment(ref _aantal);
}

/// <summary>Binnenste handler in de Airtable-pipeline: telt elke poging die echt naar Airtable gaat.</summary>
public class AirtableTellerHandler(AirtableVerbruik verbruik) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        verbruik.Tel();
        return base.SendAsync(request, ct);
    }
}
