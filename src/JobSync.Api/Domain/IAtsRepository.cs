namespace JobSync.Api.Domain;

/// <summary>Bron van vacatures (het ATS). Nu Airtable, maar de sync-logica weet dat niet.</summary>
public interface IAtsRepository
{
    Task<IReadOnlyList<Vacature>> HaalVacaturesOpAsync(CancellationToken ct = default);
    Task SchrijfTerugAsync(IReadOnlyCollection<SyncTerugkoppeling> terugkoppelingen, CancellationToken ct = default);
}
