using JobSync.Api.Domain;

namespace JobSync.Tests;

internal static class TestData
{
    public static readonly DateTimeOffset Gisteren = new(2026, 10, 26, 9, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset Vandaag = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);

    public static Vacature Vacature(
        VacatureStatus status = VacatureStatus.Gepubliceerd,
        string? externId = null,
        DateTimeOffset? laatsteSync = null,
        DateTimeOffset? gewijzigdOp = null,
        string? titel = "Backend developer .NET",
        string? locatie = "9000 Gent",
        string? contracttype = "Vast") =>
        new("rec123", titel, "Je bouwt integraties tussen ATS en vacatureplatformen.", locatie, contracttype,
            status, externId, laatsteSync, gewijzigdOp, SyncFout: null);
}
