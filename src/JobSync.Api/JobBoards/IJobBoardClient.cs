using JobSync.Api.Domain;

namespace JobSync.Api.JobBoards;

/// <summary>
/// Contract voor een vacatureplatform. De sync-logica kent enkel deze interface:
/// een echte VDAB- of Indeed-client is een nieuwe implementatie, zonder wijzigingen aan SyncService.
/// Elke implementatie mapt zelf van <see cref="Vacature"/> naar het eigen formaat.
/// </summary>
public interface IJobBoardClient
{
    /// <summary>Naam van het platform, voor logging en het dashboard.</summary>
    string Naam { get; }

    /// <summary>Publiceert de vacature en geeft het id op het platform terug.</summary>
    Task<string> PubliceerAsync(Vacature vacature, CancellationToken ct = default);

    Task WijzigAsync(string externId, Vacature vacature, CancellationToken ct = default);

    Task SluitAfAsync(string externId, CancellationToken ct = default);
}

/// <summary>
/// Een fout die het platform ons teruggeeft en die niet opgelost wordt door opnieuw te proberen
/// (validatiefout, vacature bestaat niet meer, ...). De boodschap is bedoeld voor de recruiter.
/// </summary>
public class JobBoardException(string message) : Exception(message);
