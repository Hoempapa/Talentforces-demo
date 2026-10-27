namespace JobSync.Api.Domain;

public enum VacatureStatus { Concept, Gepubliceerd, Afgesloten, Onbekend }

/// <summary>
/// Een vacature zoals ze in het ATS (Airtable) staat. Platformonafhankelijk:
/// de sync-logica en de dashboardpagina werken enkel met dit type.
/// </summary>
public record Vacature(
    string RecordId,
    string? Titel,
    string? Omschrijving,
    string? Locatie,
    string? Contracttype,
    VacatureStatus Status,
    string? ExternId,
    DateTimeOffset? LaatsteSync,
    DateTimeOffset? GewijzigdOp,
    string? SyncFout);

/// <summary>Wat er na een sync naar het ATS teruggeschreven wordt. Null-velden worden niet aangeraakt, behalve SyncFout.</summary>
public record SyncTerugkoppeling(string RecordId, string? ExternId, DateTimeOffset? LaatsteSync, string? SyncFout);
