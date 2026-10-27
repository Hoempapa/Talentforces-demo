using JobSync.Api.JobBoards;
using JobSync.Api.JobBoards.MockJobBoard;
using static JobSync.Tests.TestData;

namespace JobSync.Tests;

public class MockJobBoardMapperTests
{
    [Fact]
    public void Mapt_alle_velden_naar_platformformaat()
    {
        var request = MockJobBoardMapper.NaarRequest(Vacature());

        Assert.Equal("rec123", request.Referentie);
        Assert.Equal("Backend developer .NET", request.Functietitel);
        Assert.Equal("Je bouwt integraties tussen ATS en vacatureplatformen.", request.Beschrijving);
        Assert.Equal(new MockWerkplaats("Gent", "9000"), request.Werkplaats);
        Assert.Equal("VAST", request.Contract);
    }

    [Theory]
    [InlineData("9000 Gent", "Gent", "9000")]
    [InlineData("  2800   Mechelen ", "Mechelen", "2800")]
    [InlineData("Antwerpen", "Antwerpen", null)]
    [InlineData("Sint-Martens-Latem", "Sint-Martens-Latem", null)]
    [InlineData("", null, null)]
    [InlineData(null, null, null)]
    public void Splitst_locatie_in_postcode_en_gemeente(string? locatie, string? gemeente, string? postcode) =>
        Assert.Equal(new MockWerkplaats(gemeente, postcode), MockJobBoardMapper.NaarWerkplaats(locatie));

    [Theory]
    [InlineData("Vast", "VAST")]
    [InlineData("tijdelijk", "TIJDELIJK")]
    [InlineData("Interim", "INTERIM")]
    [InlineData("Freelance", "FREELANCE")]
    [InlineData("Studentenjob", "STUDENT")]
    [InlineData(" Vast ", "VAST")]
    public void Vertaalt_contracttype(string invoer, string verwacht) =>
        Assert.Equal(verwacht, MockJobBoardMapper.NaarContract(invoer));

    [Fact]
    public void Leeg_contracttype_wordt_null_zodat_het_platform_het_als_verplicht_meldt() =>
        Assert.Null(MockJobBoardMapper.NaarContract("  "));

    [Fact]
    public void Onbekend_contracttype_geeft_duidelijke_fout()
    {
        var ex = Assert.Throws<JobBoardException>(() => MockJobBoardMapper.NaarContract("Bijbaan"));
        Assert.Contains("Bijbaan", ex.Message);
    }

    [Fact]
    public void Titel_wordt_getrimd_maar_niet_ingekort()
    {
        // Bewuste keuze: een te lange titel stil afkappen verandert de vacature. Het platform weigert hem
        // en de recruiter ziet de fout in Airtable.
        var lang = new string('x', 120);
        Assert.Equal(lang, MockJobBoardMapper.NaarRequest(Vacature(titel: $"  {lang}  ")).Functietitel);
    }
}
