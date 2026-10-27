using JobSync.Api.Domain;
using JobSync.Api.JobBoards;
using JobSync.Api.Sync;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using static JobSync.Tests.TestData;

namespace JobSync.Tests;

/// <summary>Volledige sync-ronde met nep-implementaties van het ATS en het platform.</summary>
public class SyncServiceTests
{
    private readonly FakeAts _ats = new();
    private readonly FakePlatform _platform = new();
    private readonly SyncService _sync;

    public SyncServiceTests()
    {
        var services = new ServiceCollection()
            .AddSingleton<IAtsRepository>(_ats)
            .AddSingleton<IJobBoardClient>(_platform)
            .BuildServiceProvider();
        _sync = new SyncService(services.GetRequiredService<IServiceScopeFactory>(), new SyncLog(NullLogger<SyncLog>.Instance));
    }

    [Fact]
    public async Task Publiceert_en_schrijft_ExternId_terug()
    {
        _ats.Vacatures.Add(Vacature());

        var rapport = await _sync.SynchroniseerAsync("test");

        Assert.Equal(1, rapport!.Gepubliceerd);
        var terug = Assert.Single(_ats.Teruggeschreven);
        Assert.Equal("EXT-1", terug.ExternId);
        Assert.NotNull(terug.LaatsteSync);
        Assert.Null(terug.SyncFout);
    }

    [Fact]
    public async Task Fout_op_platform_wordt_fout_in_ATS_zonder_LaatsteSync()
    {
        _ats.Vacatures.Add(Vacature());
        _platform.Fout = new JobBoardException("Titel te lang");

        var rapport = await _sync.SynchroniseerAsync("test");

        Assert.Equal(1, rapport!.Fouten);
        var terug = Assert.Single(_ats.Teruggeschreven);
        Assert.Null(terug.LaatsteSync); // zodat de volgende ronde het opnieuw probeert
        Assert.Contains("Titel te lang", terug.SyncFout);
    }

    [Fact]
    public async Task Ongewijzigde_vacatures_worden_niet_aangeraakt()
    {
        _ats.Vacatures.Add(Vacature(VacatureStatus.Concept));
        _ats.Vacatures.Add(Vacature(VacatureStatus.Gepubliceerd, "EXT-9", laatsteSync: Vandaag, gewijzigdOp: Gisteren));

        var rapport = await _sync.SynchroniseerAsync("test");

        Assert.Equal(2, rapport!.Ongewijzigd);
        Assert.Empty(_platform.Aanroepen);
        Assert.Empty(_ats.Teruggeschreven);
    }

    private class FakeAts : IAtsRepository
    {
        public List<Vacature> Vacatures { get; } = [];
        public List<SyncTerugkoppeling> Teruggeschreven { get; } = [];

        public Task<IReadOnlyList<Vacature>> HaalVacaturesOpAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Vacature>>(Vacatures);

        public Task SchrijfTerugAsync(IReadOnlyCollection<SyncTerugkoppeling> t, CancellationToken ct = default)
        {
            Teruggeschreven.AddRange(t);
            return Task.CompletedTask;
        }
    }

    private class FakePlatform : IJobBoardClient
    {
        public Exception? Fout { get; set; }
        public List<string> Aanroepen { get; } = [];
        public string Naam => "Fake";

        public Task<string> PubliceerAsync(Vacature v, CancellationToken ct = default) => Doe($"publiceer {v.RecordId}", "EXT-1");
        public Task WijzigAsync(string externId, Vacature v, CancellationToken ct = default) => Doe($"wijzig {externId}", "");
        public Task SluitAfAsync(string externId, CancellationToken ct = default) => Doe($"sluit {externId}", "");

        private Task<string> Doe(string aanroep, string resultaat)
        {
            Aanroepen.Add(aanroep);
            return Fout is null ? Task.FromResult(resultaat) : Task.FromException<string>(Fout);
        }
    }
}
