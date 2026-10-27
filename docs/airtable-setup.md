# Airtable klaarzetten voor de demo (± 15 minuten)

Een gratis Airtable-account volstaat. De Airtable-interface is in het Engels, dus de
knoppen hieronder staan ook in het Engels.

## 1. Account en base

1. Maak een gratis account aan op <https://airtable.com/signup>.
2. Maak een nieuwe, lege base: **Create** → **Build an app on your own** / **Start from scratch**
   (de naam van die knop verschilt soms). Noem de base bv. `Talentforces demo`.

## 2. Tabel importeren

1. Klik in de base op **+ Add or import** → **CSV file** en kies
   [`docs/airtable/Vacatures.csv`](airtable/Vacatures.csv) uit deze repo.
2. Kies om er een **nieuwe tabel** van te maken. Hij moet exact **`Vacatures`** heten:
   hernoem hem als dat nodig is.
3. Verwijder de lege standaardtabel (`Table 1`) als die er nog staat.

## 3. Veldtypes instellen

Na de import is alles tekst. Klik per veld op de pijl naast de naam → **Edit field** en
zet het type zo. **Let op hoofdletters en spaties:** JobSync zoekt de velden op hun exacte naam.

| Veld | Type | Instelling |
|---|---|---|
| Titel | Single line text | (primair veld) |
| Omschrijving | Long text | |
| Locatie | Single line text | |
| Contracttype | Single select | opties worden automatisch aangemaakt uit de data: Vast, Tijdelijk, Interim, Freelance, Studentenjob |
| Status | Single select | opties: Concept, Gepubliceerd, Afgesloten |
| ExternId | Single line text | |
| LaatsteSync | Date | **Include time** aan, **Use the same time zone (GMT) for all collaborators** aan |
| SyncFout | Long text | |

Voeg daarna één nieuw veld toe (**+** rechts van de laatste kolom):

| Veld | Type | Instelling |
|---|---|---|
| GewijzigdOp | Last modified time | kies **Specific fields** en vink enkel aan: Titel, Omschrijving, Locatie, Contracttype, Status. **Include time** aan. |

> Waarom "Specific fields"? JobSync schrijft zelf ExternId, LaatsteSync en SyncFout terug.
> Als `GewijzigdOp` ook naar die velden kijkt, lijkt elke vacature na elke sync "gewijzigd"
> en wordt ze telkens opnieuw aangepast.

Tip voor de demo: geef de kolommen ExternId, LaatsteSync, SyncFout en GewijzigdOp een
andere kleur of zet ze rechts, zodat het publiek ziet welke velden JobSync invult.

## 4. Personal access token

1. Ga naar <https://airtable.com/create/tokens> → **Create token**.
2. Naam: `JobSync demo`.
3. **Scopes**: `data.records:read` en `data.records:write`. Meer is niet nodig.
4. **Access**: voeg enkel de base `Talentforces demo` toe.
5. Klik **Create token** en kopieer het token (begint met `pat…`). Je ziet het maar één keer.

## 5. Base-id

Open de base in je browser. De URL ziet eruit als
`https://airtable.com/appAbC123xyz/tbl…/viw…`. Het deel dat met **`app`** begint, is de base-id.

## 6. Secrets instellen en starten

```bash
cd src/JobSync.Api
dotnet user-secrets set "Airtable:Token"  "pat..."
dotnet user-secrets set "Airtable:BaseId" "app..."
dotnet user-secrets set "MockJobBoard:ClientSecret" "mock-secret-niet-echt"
```

Start daarna beide API's (zie de README). Het label bovenaan het dashboard toont
**Airtable gekoppeld** in plaats van "Demo-modus".

## Gaat er iets mis?

Het dashboard toont de fout van Airtable met een tip erbij:

| Fout | Oorzaak |
|---|---|
| 401 | Token fout gekopieerd |
| 403 | Scopes ontbreken, of de base is niet toegevoegd aan het token |
| 404 | Base-id fout, of de tabel heet niet exact `Vacatures` |
| 422 `UNKNOWN_FIELD_NAME` | Een veldnaam klopt niet (let op hoofdletters) |
| 422 `INVALID_VALUE_FOR_COLUMN` | Verkeerd veldtype, meestal LaatsteSync die geen Date-veld is |

## Limieten van het gratis plan

Het gratis plan laat **1.000 API-calls per maand** toe (per workspace) en 1.000 records per base.
JobSync is daarop afgesteld:

- 1 sync = 1 call om te lezen + 1 call per 10 gewijzigde vacatures om terug te schrijven.
- 1 keer het dashboard openen of op **Vernieuwen** klikken = 1 call.
- De automatische sync draait elke 15 minuten, zo'n 4 tot 8 calls per uur zolang de app draait.
  Een dag voorbereiden en een demo geven blijft zo ruim onder de limiet.
- Het dashboard toont hoeveel Airtable-calls er sinds de start gedaan zijn.
- Laat de app niet dagenlang openstaan. Of zet de automatische sync uit met
  `dotnet run --project src/JobSync.Api -- --Sync:Automatisch=false`.

Actuele limieten: <https://support.airtable.com/docs/managing-api-call-limits-in-airtable>.
