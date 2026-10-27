using JobSync.Api.Domain;

namespace JobSync.Api.Sync;

public enum SyncActie { Geen, Publiceren, Aanpassen, Afsluiten }

/// <summary>
/// Beslist per vacature wat er op het platform moet gebeuren. Pure functie: geen I/O, volledig testbaar.
/// </summary>
public static class SyncBeslisser
{
    public static SyncActie Bepaal(Vacature v) => v.Status switch
    {
        VacatureStatus.Gepubliceerd when v.ExternId is null => SyncActie.Publiceren,
        VacatureStatus.Gepubliceerd when GewijzigdSindsSync(v) => SyncActie.Aanpassen,
        VacatureStatus.Afgesloten when v.ExternId is not null && GewijzigdSindsSync(v) => SyncActie.Afsluiten,
        _ => SyncActie.Geen
    };

    /// <summary>
    /// Nooit gesynct, of inhoud/status gewijzigd na de laatste geslaagde sync.
    /// "GewijzigdOp" is een Airtable "Last modified time"-veld dat enkel naar de inhoudsvelden kijkt,
    /// zodat ons eigen terugschrijven (ExternId, LaatsteSync, SyncFout) geen nieuwe sync uitlokt.
    /// </summary>
    private static bool GewijzigdSindsSync(Vacature v) =>
        v.LaatsteSync is null || (v.GewijzigdOp is not null && v.GewijzigdOp > v.LaatsteSync);
}
