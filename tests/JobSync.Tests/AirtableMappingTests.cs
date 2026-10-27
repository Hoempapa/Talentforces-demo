using JobSync.Api.Ats;
using JobSync.Api.Domain;

namespace JobSync.Tests;

public class AirtableMappingTests
{
    private static AirtableRecord<VacatureVelden> Record(string? status, string? externId = null) =>
        new("recA", new VacatureVelden("Titel", "Omschrijving", "Gent", "Vast", status, externId, null, null, null));

    [Theory]
    [InlineData("Gepubliceerd", VacatureStatus.Gepubliceerd)]
    [InlineData("Concept", VacatureStatus.Concept)]
    [InlineData("Afgesloten", VacatureStatus.Afgesloten)]
    [InlineData("In review", VacatureStatus.Onbekend)]
    [InlineData(null, VacatureStatus.Onbekend)]
    public void Leest_status(string? airtableStatus, VacatureStatus verwacht) =>
        Assert.Equal(verwacht, AirtableClient.NaarDomein(Record(airtableStatus)).Status);

    [Theory]
    [InlineData("  ", null)]
    [InlineData(" MJB-1 ", "MJB-1")]
    public void Lege_ExternId_wordt_null(string? externId, string? verwacht) =>
        Assert.Equal(verwacht, AirtableClient.NaarDomein(Record("Gepubliceerd", externId)).ExternId);

    [Fact]
    public void Geslaagde_sync_schrijft_ExternId_en_LaatsteSync_en_wist_fout()
    {
        var update = AirtableClient.NaarUpdateRecord(
            new SyncTerugkoppeling("recA", "MJB-1", new DateTimeOffset(2026, 10, 27, 10, 0, 0, TimeSpan.FromHours(1)), null));

        Assert.Equal("MJB-1", update.Fields["ExternId"]);
        Assert.Equal("2026-10-27T09:00:00.0000000Z", update.Fields["LaatsteSync"]);
        Assert.True(update.Fields.ContainsKey("SyncFout"));
        Assert.Null(update.Fields["SyncFout"]);
    }

    [Fact]
    public void Mislukte_sync_schrijft_enkel_de_fout()
    {
        var update = AirtableClient.NaarUpdateRecord(new SyncTerugkoppeling("recA", null, null, "Kapot"));

        Assert.Equal(["SyncFout"], update.Fields.Keys);
        Assert.Equal("Kapot", update.Fields["SyncFout"]);
    }
}
