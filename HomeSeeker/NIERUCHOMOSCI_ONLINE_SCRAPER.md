# HomeSeeker – scraper nieruchomosci-online.pl (analiza wykonalności)

Status: **zaimplementowano** (`Scrapers/NieruchomosciOnlineScraper.cs`, testy
`NieruchomosciOnlineScraperTests`). Data rozpoznania: 2026-10-18.
Przed włączeniem na produkcji sprawdzić dostęp z IP Azure (patrz „Ryzyka”); w razie blokady
dodać `Nieruchomosci-online` do `HomeSeeker:DisabledPortals`.

## Werdykt

**Wykonalne i stosunkowo proste.** Portal renderuje listy po stronie serwera, a w HTML jest
osadzony kompletny JSON z danymi kafelków (cena, metraż, pokoje, rynek, lokalizacja).
Wyszukiwarka przyjmuje filtry ceny i metrażu w URL, a strona ogłoszenia zawiera współrzędne
w JSON-LD (`GeoCoordinates`). Nowy scraper zmieści się w `ListingScraperBase` bez zmian
w reszcie systemu (backend i aplikacja pobierają listę portali z `GET /api/homeseeker/portals`).

Główne ryzyko: tak jak w OLX, blokada adresu IP Azure (niesprawdzone, testy były z lokalnego IP).

## Dostęp i robots.txt

- Zwykły `GET` z przeglądarkowym User-Agent zwraca 200; nie zaobserwowano challenge’a anty-botowego.
- `robots.txt` **nie ma sekcji `User-agent: *`**. Reguły są tylko dla konkretnych botów:
  - Googlebot – zablokowane m.in. `?o=`, `&o=`, `?c*`, `?op*`, `&kat*`, `&pkat*`, `&f*`, `&idg*`,
    `&log*`, `&sort*`, `?pn*`, `?secCode*`;
  - Bingbot – `Crawl-delay: 1`;
  - boty SEO (Ahrefs, MJ12, Yandex …) – całkowicie zablokowane.
- Zalecenie: trzymać się zasad Googlebota (nie używać sortowania ani parametrów z listy
  powyżej), zachować `RequestDelay` (≥ 3 s) i limit `MaxSearchPagesPerPortal`.

## Adresy URL

### Wyszukiwanie z filtrami (rekomendowane)

```
https://www.nieruchomosci-online.pl/szukaj.html?3,{typ},sprzedaz,,{Miasto},,,,{cenaOd}-{cenaDo},{m2Od}-{m2Do}&p={strona}
```

- `{typ}`: `mieszkanie` | `dom`.
- `{Miasto}`: nazwa w formie URL-encoded (np. `Krak%C3%B3w`, `Wroc%C5%82aw`). Opcjonalny sufiks
  `:{idLokalizacji}` (np. `Kraków:5600`) nie jest wymagany, bo wynik jest ten sam.
- Zakresy mogą być otwarte: `-1500000`, `100-`, puste pole = brak filtra.
- `&p=N` – stronicowanie (zweryfikowano `p=2`).
- Przykładowe wyniki z testów:

| Zapytanie | Ofert | Stron |
|-----------|-------|-------|
| Kraków, mieszkania 500–800 tys., 40–70 m² | 2 071 | 53 |
| Kraków, domy do 1,5 mln, od 100 m² | 159 | 9 |
| Wrocław, mieszkania bez filtrów | 4 974 | 124 |

- Na jednej stronie jest 46–47 kafelków. Domyślne sortowanie to prawdopodobnie data
  dodania (`ord: "dd"` w JSON), więc nowe oferty są na pierwszych stronach.

### Listy strukturalne (alternatywa bez filtrów)

- `https://{miasto}.nieruchomosci-online.pl/mieszkania/` oraz `/domy/`, `mieszkania,sprzedaz/`,
  stronicowanie `?p=N`. **Ignorują** parametry `priceFrom`/`areaFrom`.
- Kraków `/mieszkania/`: 9 679 ofert (sprzedaż i wynajem razem); `/mieszkania,sprzedaz/`: 7 460.

### Ogłoszenia

- Zwykłe: `https://{miasto}.nieruchomosci-online.pl/{typ-slug},{tytul-slug}/{id}.html`, gdzie
  `typ-slug` to np. `mieszkanie`, `nowe-mieszkanie`, `dom`, `nowy-dom`, `dom-wolnostojacy`.
- Lokal z inwestycji deweloperskiej może kanonicznie wskazywać na stronę inwestycji
  (`/wzgorze-gustawa.html`). Dlatego `ExternalId` trzeba brać z id lokalu, nie z URL kanonicznego.
- Wyniki dla Krakowa obejmują też pobliskie miejscowości z innych subdomen (np. `niepolomice`).

## Źródła danych na stronie listy

1. **`NOTrafficEventBuilder.buildCachedProps({...});`** – JSON w `<script>` (**najlepsze źródło**).
   - `list_props`: `tiles` (liczba ofert), `pgpg` (bieżąca strona), `pgpgs` (liczba stron),
     `loccta` (miasto), `lctrn` (`sale`), `lccata` (`flats`/`houses`).
   - `record_props` zawiera wszystkie kafelki strony. Klucz `a{id}` oznacza zwykłe ogłoszenie,
     a `i{idInwestycji}_{id}` lokal z inwestycji. Pola:

| Pole | Znaczenie |
|------|-----------|
| `prc` | cena (zł) – obecna także dla lokali z inwestycji |
| `rsur` | powierzchnia m² |
| `rooms` | liczba pokoi |
| `rcmrkt` | `primary` / `secondary` |
| `rccata` | `flats` / `houses` |
| `rctrn` | `sale` / `rent` |
| `rloccta` | miasto (slug) |
| `rlocqtn` | dzielnica |
| `rlocstsn` | ulica |
| `rcrest` | `ad` / `investment_ad` |
| `pbh` | `dev` / `n-dev` (deweloper czy nie) |

   - Brak URL i tytułu, więc trzeba je dołączyć z HTML kafelka po id.
2. **Kafelki HTML**: `div.tile[data-id="a27030818"]` z atrybutem `data-market-type`;
   `h2.name a[href]` (tytuł i URL), `p.province` (dzielnica, miasto), `p.primary-display`
   (cena, `span.area`, cena za m²) oraz atrybuty (piętro, pokoje, rok budowy).
   Można je parsować przez HtmlAgilityPack (już używany w `OlxScraper`), także jako fallback.
3. **JSON-LD `CollectionPage`** (`mainEntity.offers[0].offers[]`) zawiera `url`, `price`, `floorSize`,
   `numberOfRooms`, `address`, `description`. Obejmuje jednak tylko ok. 39 z 47 kafelków,
   a lokale deweloperskie mają tu puste cenę i metraż. Nadaje się wyłącznie jako zapasowe źródło URL.

## Strona ogłoszenia (szczegóły)

- Rozmiar ok. 120–190 KB (lista ma ok. 1,1 MB).
- JSON-LD `Apartment` (lub odpowiednik dla domu) zawiera `name`, `description` (krótki, generowany),
  `image[]`, `amenityFeature[]` (parking, balkon, winda, ogród …), `additionalProperty[]` (pokoje …)
  oraz **`geo: { latitude, longitude }`**. To pozwala uzupełnić `Latitude`/`Longitude` w mapie
  bez geokodowania.
- Pełny opis jest w `#boxCustomDescWrapper` (nagłówek „Opis nieruchomości”). Treść jest w HTML
  z encjami (`&nbsp;`) i z obciętą wersją „(...) Rozwiń opis”, więc należy brać pełny blok.
- Uwaga na jakość danych: w jednym teście tytuł podawał „Kraków, Nowy Świat”, a opis i współrzędne
  wskazywały gminę Wieliczka. Odległość liczona z `geo` jest wiarygodniejsza niż adres w tytule.

## Proponowana implementacja

```csharp
public sealed partial class NieruchomosciOnlineScraper : ListingScraperBase
{
    public override string PortalName => PortalNames.NieruchomosciOnline;   // "Nieruchomosci-online"
    protected override string PortalDomain => "nieruchomosci-online.pl"; // subdomeny akceptuje IsUrlOnDomain

    protected override string BuildSearchUrl(SearchProfile profile, int page) => ...; // szukaj.html?3,...&p=
    protected override SearchPage ParseSearchPage(string html, SearchProfile profile) => ...;
    protected override ListingDetails? ParseDetailsPage(string html, string url) => ...;
}
```

1. `PortalNames.NieruchomosciOnline` – nowa stała.
2. `BuildSearchUrl`: typ wg `profile.PropertyType`, miasto przez `Uri.EscapeDataString`, zakres ceny
   z budżetu profilu i zakres metrażu z `MinAreaSqm`/`MaxAreaSqm`, a dla `page > 1` dopisane `&p={page}`.
3. `ParseSearchPage`:
   - wyciągnąć JSON regexem `buildCachedProps\((\{.*?\})\);` i zdeserializować `System.Text.Json`;
   - dla każdego wpisu `record_props` z `rctrn == "sale"` znaleźć kafelek `div.tile[data-id=klucz]`
     i pobrać z niego URL oraz tytuł;
   - `ExternalId` = numeryczne id (część po `a` lub po `_`);
   - `DevelopmentKey` = `"no:{idInwestycji}"` dla kluczy `i…`, co pozwala na istniejącą deduplikację inwestycji;
   - koniec stronicowania gdy `pgpg >= pgpgs`;
   - dodatkowy filtr ceny i metrażu po stronie klienta, na wszelki wypadek.
4. `ParseDetailsPage`: JSON-LD (`geo` → `ParseCoordinates`, `amenityFeature`, `additionalProperty`)
   oraz pełny opis z `#boxCustomDescWrapper` (HtmlAgilityPack + `HtmlEntity.DeEntitize`).
5. Rejestracja: jedna linia w `HomeSeekerExtensions` (`AddHttpClient`/`AddScoped<IListingScraper, …>`
   wg komentarza). Frontend pobierze portal automatycznie przez `/portals`, a wyłączenie
   bez deployu jest możliwe przez `HomeSeeker:DisabledPortals`.

## Mapowanie na `ScrapedListing`

| `ScrapedListing` | Źródło |
|------------------|--------|
| `ExternalId` | id z klucza `record_props` |
| `Url` | `h2.name a[href]` (fallback: JSON-LD `url`) |
| `Title` | tekst linku kafelka |
| `Price` | `prc` |
| `AreaSqm` | `rsur` |
| `Rooms` | `rooms` |
| `Location` | `rlocstsn`, `rlocqtn`, `rloccta` |
| `PropertyType` | `rccata` (`flats` → mieszkanie, `houses` → dom) |
| `DevelopmentKey` | `no:{idInwestycji}` dla lokali z inwestycji |
| Szczegóły / współrzędne | strona ogłoszenia (JSON-LD `geo`, opis) |

## Ryzyka

- **Blokada IP Azure** – OLX już zwraca 403 z Container Apps. Przed wdrożeniem trzeba zrobić
  test z produkcji (np. jednorazowy request z kontenera). W razie blokady portal można wyłączyć
  przez `DisabledPortals` albo skanować lokalnie i synchronizować dane (jak przy OLX).
- **Rozmiar stron** – ok. 1,1 MB na stronę listy, czyli przy `MaxSearchPagesPerPortal = 5`
  ok. 5,5 MB na skan. To akceptowalne, ale warto wyciąć tylko potrzebny fragment przed parsowaniem
  HTML (regex na JSON, HtmlAgilityPack tylko na kafelki).
- **Pobliskie miejscowości** w wynikach (np. Niepołomice dla Krakowa). Można je odfiltrować
  wg `rloccta` albo zostawić i polegać na odległości od centrum lub od lokalizacji użytkownika.
- **Rynek pierwotny**: dużo lokali deweloperskich (na stronie testowej ok. 40%), z ceną i metrażem
  per lokal. Wchodzą do median tak jak inne ogłoszenia; można rozważyć osobne traktowanie
  (patrz wcześniejsze notatki o rynekpierwotny.pl).
- **Duplikaty z Otodom/OLX** – deweloperzy i biura publikują wszędzie. Obecna deduplikacja działa
  w obrębie portalu i inwestycji, więc duplikaty międzyportalowe pozostaną (temat ogólny,
  niezależny od tego scrapera).
- **Zmiany struktury** – `buildCachedProps` to wewnętrzny obiekt analityczny portalu. Fallback na
  kafelki HTML i JSON-LD ogranicza skutki zmian.

## Plan testów

- Fixture’y HTML (przycięte) w `ReceptyOks_UnitTests/HomeSeeker/Fixtures/`: strona wyszukiwania
  z ogłoszeniem zwykłym i lokalem z inwestycji, ostatnia strona, strona ogłoszenia z `geo`.
- Testy wzorowane na `OlxScraperTests`/`OtodomScraperTests`:
  - `BuildSearchUrl`: kodowanie miasta, otwarte zakresy, `&p=` od strony 2;
  - `ParseSearchPage`: liczba ofert, mapowanie pól, `DevelopmentKey`, pomijanie `rent`, koniec stron;
  - fallback, gdy brak `buildCachedProps`;
  - `ParseDetailsPage`: opis bez encji, współrzędne, `IsUrlOnDomain` dla subdomen;
  - integracja z `ListingScraperBase` (403 zatrzymuje portal, częściowy wynik).

## Otwarte pytania

- Czy pobliskie miejscowości mają zostać w wynikach, czy filtrujemy ściśle po mieście profilu?
- Czy filtr pokoi (`rooms`) jest potrzebny w URL? Format pozostałych pól `szukaj.html` nie został
  rozpoznany; na razie wystarczy filtr po stronie klienta.
- Czy przed implementacją robimy test dostępności z IP Azure?
