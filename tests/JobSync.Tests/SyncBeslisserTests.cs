using JobSync.Api.Domain;
using JobSync.Api.Sync;
using static JobSync.Tests.TestData;

namespace JobSync.Tests;

public class SyncBeslisserTests
{
    [Fact]
    public void Gepubliceerd_zonder_ExternId_wordt_gepubliceerd() =>
        Assert.Equal(SyncActie.Publiceren, SyncBeslisser.Bepaal(Vacature(VacatureStatus.Gepubliceerd)));

    [Fact]
    public void Gepubliceerd_en_gewijzigd_na_laatste_sync_wordt_aangepast() =>
        Assert.Equal(SyncActie.Aanpassen, SyncBeslisser.Bepaal(
            Vacature(VacatureStatus.Gepubliceerd, "MJB-1", laatsteSync: Gisteren, gewijzigdOp: Vandaag)));

    [Fact]
    public void Gepubliceerd_en_niet_gewijzigd_sinds_sync_doet_niets() =>
        Assert.Equal(SyncActie.Geen, SyncBeslisser.Bepaal(
            Vacature(VacatureStatus.Gepubliceerd, "MJB-1", laatsteSync: Vandaag, gewijzigdOp: Gisteren)));

    [Fact]
    public void Gepubliceerd_met_ExternId_maar_nooit_gesynct_wordt_aangepast() =>
        // Bv. ExternId manueel ingevuld in Airtable: veiliger om één keer te pushen.
        Assert.Equal(SyncActie.Aanpassen, SyncBeslisser.Bepaal(
            Vacature(VacatureStatus.Gepubliceerd, "MJB-1", laatsteSync: null, gewijzigdOp: Gisteren)));

    [Fact]
    public void Afgesloten_met_ExternId_en_statuswijziging_na_sync_wordt_afgesloten() =>
        Assert.Equal(SyncActie.Afsluiten, SyncBeslisser.Bepaal(
            Vacature(VacatureStatus.Afgesloten, "MJB-1", laatsteSync: Gisteren, gewijzigdOp: Vandaag)));

    [Fact]
    public void Afgesloten_en_al_afgesloten_bij_vorige_sync_doet_niets() =>
        Assert.Equal(SyncActie.Geen, SyncBeslisser.Bepaal(
            Vacature(VacatureStatus.Afgesloten, "MJB-1", laatsteSync: Vandaag, gewijzigdOp: Gisteren)));

    [Fact]
    public void Afgesloten_zonder_ExternId_was_nooit_online_en_doet_niets() =>
        Assert.Equal(SyncActie.Geen, SyncBeslisser.Bepaal(Vacature(VacatureStatus.Afgesloten)));

    [Theory]
    [InlineData(VacatureStatus.Concept, null)]
    [InlineData(VacatureStatus.Concept, "MJB-1")]
    [InlineData(VacatureStatus.Onbekend, null)]
    [InlineData(VacatureStatus.Onbekend, "MJB-1")]
    public void Concept_of_onbekende_status_doet_niets(VacatureStatus status, string? externId) =>
        Assert.Equal(SyncActie.Geen, SyncBeslisser.Bepaal(Vacature(status, externId, gewijzigdOp: Vandaag)));
}
