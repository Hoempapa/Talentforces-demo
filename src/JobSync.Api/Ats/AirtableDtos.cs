using System.Text.Json.Serialization;

namespace JobSync.Api.Ats;

// DTO's die 1-op-1 het JSON-formaat van de Airtable REST API volgen.
// Veldnamen = kolomnamen in de tabel "Vacatures".

public record AirtableLijst<T>(
    [property: JsonPropertyName("records")] List<AirtableRecord<T>> Records,
    [property: JsonPropertyName("offset")] string? Offset);

public record AirtableRecord<T>(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("fields")] T Fields);

public record VacatureVelden(
    [property: JsonPropertyName("Titel")] string? Titel,
    [property: JsonPropertyName("Omschrijving")] string? Omschrijving,
    [property: JsonPropertyName("Locatie")] string? Locatie,
    [property: JsonPropertyName("Contracttype")] string? Contracttype,
    [property: JsonPropertyName("Status")] string? Status,
    [property: JsonPropertyName("ExternId")] string? ExternId,
    [property: JsonPropertyName("LaatsteSync")] DateTimeOffset? LaatsteSync,
    [property: JsonPropertyName("GewijzigdOp")] DateTimeOffset? GewijzigdOp,
    [property: JsonPropertyName("SyncFout")] string? SyncFout);

/// <summary>Body voor PATCH: velden als dictionary, zodat we enkel meesturen wat we willen wijzigen.</summary>
public record AirtableUpdate(
    [property: JsonPropertyName("records")] List<AirtableUpdateRecord> Records);

public record AirtableUpdateRecord(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("fields")] Dictionary<string, object?> Fields);
