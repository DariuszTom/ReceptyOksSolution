# HomeSeeker — AI Real-Estate Market Scanner

Plan implementacji i projekt architektury agenta AI skanującego rynek nieruchomości
w wybranym mieście (portale polskie: **Otodom**, **OLX**) w poszukiwaniu dobrych domów.

## 1. Cel i założenia

| Decyzja | Wybór |
|---|---|
| Architektura | `HomeSeeker` = biblioteka klas z całą logiką domenową; `ReceptyOks.Api` (już wdrożone na Azure Container Apps) referencuje ją, hostuje cykliczne skany i wystawia endpointy — reużycie Key Vault, bazy, autoryzacji i pipeline'u wdrożeniowego |
| Pozyskiwanie danych | Deterministyczne scrapery per portal (Otodom, OLX) zbierają kandydatów; `AiAgent` ocenia najlepszych **wyłącznie na podstawie zescrapowanych metadanych** (bez `WebBrowsingTool` — wyłączony ze względów bezpieczeństwa, CODE_REVIEW.md C4) |
| Wyzwalanie | **Jeden wspólny profil domowy** (max ~3 użytkowników) ustawiany przez `PUT /profile` (miasto, metraż, cena, kryteria wolnym tekstem); `BackgroundService` skanuje go cyklicznie wg harmonogramu z konfiguracji; **brak skanu na żądanie** |
| Wynik | Ocenione ogłoszenia w bazie (deduplikacja między skanami) + **raport HTML z szablonu w kodzie** (nie z AI — CODE_REVIEW.md C3) wysyłany mailem tylko, gdy są nowe ogłoszenia lub obniżki |

## 2. Architektura

```
┌────────────────────────────────────────────────────────────────────────────┐
│ ReceptyOks.Api (Azure Container App)                                       │
│                                                                            │
│  HomeSeekerEndpoints          HomeSeekerScanService (BackgroundService)    │
│  /api/homeseeker/*                 │ PeriodicTimer (jeden wspólny profil)  │
│        │                           ▼                                       │
│        │                    IMarketScanService ◄──────────────┐            │
│        ▼                           │                          │            │
│  SearchProfileRequestValidator     │ orkiestracja skanu       │            │
│  (FluentValidation)                ▼                          │            │
│                     ┌──────────────┴───────────────┐          │            │
│                     ▼                              ▼          │            │
│              IListingScraper[]              IListingEvaluator │            │
│              (Otodom, OLX)                  (AiAgent, bez narzędzi)        │
│                     │                              │          │            │
│                     ▼                              ▼          ▼            │
│              IListingRepository ──── HomeSeekerDbContext ── IScanReport-   │
│              (upsert + dedup)        (SQLite dev/Azure SQL) Sender (MailKit)│
└────────────────────────────────────────────────────────────────────────────┘
         HomeSeeker (biblioteka): modele, scrapery, ewaluacja, orkiestracja
         ReceptyOks.Shared: AiAgent, AnthropicAgent, MailSender (MailKit)
```

**Kierunek zależności:** `HomeSeeker` → `ReceptyOks.Shared`; `ReceptyOks.Api` → `HomeSeeker`.
EF Core, hosting i endpointy zostają w Api (spójnie z `RecipeDbContext` w `ReceptyOks.Api/DbUtility`);
HomeSeeker trzyma logikę domenową za interfejsami (`Abstractions/`), więc jest wolny od EF i testowalny jednostkowo.

**Przepływ pojedynczego skanu:**

```
lease (TryAcquireScanLeaseAsync) → ScanRun(Running) → scrapery (try/catch per portal; wszystkie padły → Failed)
  → upsert/dedup po (profil, portal, externalId)
  → wybór kandydatów: tylko nowe lub z obniżką ceny, ranking po cenie/m², limit MaxCandidatesPerScan
  → ocena sekwencyjna agentem na metadanych (JSON: score, summary, pros, cons, priceAssessment)
  → raport HTML z szablonu w kodzie (HTML-encoding, bezpieczne URL-e)
  → e-mail do NotificationEmail (tylko przy nowych/obniżkach) → ScanRun(Completed/Failed, liczniki, ReportHtml)
  → ReleaseScanLeaseAsync (LastScannedAt stemplowany tylko przy sukcesie)
```

## 3. Infrastruktura do reużycia (zweryfikowana w kodzie)

- `ReceptyOks.Shared/AI/AiAgent.cs` — `ChatAsync<T>` (strukturalny JSON), `AddTool*`; **stanowy (sesja per instancja) → tworzenie per ocena przez fabrykę**.
- `ReceptyOks.Shared/AI/AnthropicAgent.cs` + `AnthropicSettings.cs` — `IChatClient` z Anthropic SDK; klucz API dostępny po stronie serwera jako `configuration["Token"]` (Key Vault przez `SecretsResolver`, patrz `TokenProviderEndpoints.cs:48`).
- `ReceptyOks.Shared/AI/WebBrowsingTool.cs` — **nieużywany** przez HomeSeeker (SSRF / prompt injection, CODE_REVIEW.md C4); ewentualny powrót wymaga fetchera z allow-listą i walidacją DNS.
- `ReceptyOks.Shared/MessegeSender/MailSender.cs` — ✅ przepisany na **MailKit** (async `MailConfig`/`SendMail`, `IDisposable`, timeout 20 s); hasło jako `SecureString` (rozszerzenie `ToSecureString()` w `ReceptyOks.Shared.Misc`).
- Wzorce w `ReceptyOks.Api`: grupy endpointów (`Endpoints/RecipeEndpoints.cs`), `BackgroundService` + `PeriodicTimer` (`Middleware/ShoppingListCleaner.cs`), poolowany DbContext SQLite dev / Azure SQL prod (`Extensions/DatabaseExtensions.cs`), FluentValidation, rate limitery `"fixed"`/`"strict"`, auth X-Api-Key działa automatycznie.
- `AddServiceDefaults()` już dodaje standardową odporność (retry/timeout/circuit-breaker) do wszystkich klientów `IHttpClientFactory` — bez dodatkowego Polly.

## 4. Fazy implementacji

### Faza 1 — biblioteka HomeSeeker ✅ (zaimplementowana)

- **`HomeSeeker.csproj`** ✅: referencja do `ReceptyOks.Shared`; pakiety `HtmlAgilityPack`, `Microsoft.Extensions.Options/Http/Logging.Abstractions`; `InternalsVisibleTo ReceptyOks_UnitTests`.
- **`Models/`** ✅ (wzorzec soft-delete `IsDeleted` + `UpdatedAt` jak `Recipe.cs`):
  NotificationEmail (1–5 adresów), IsActive, LastScannedAt, ScanLeaseUntil/ScanLeaseToken (lease skanu z tokenem — zwolnienie tylko przez właściciela lease), OwnerId (= kto ostatnio edytował).
    - `NotificationEmails` — parsowanie/normalizacja listy adresów rozdzielonych `,`/`;`; `UpsertResult` (IsNew/PriceDropped); `ScrapeResult`.
  - `HouseListing` — jednostka deduplikacji: Portal, ExternalId, Url, Price, PreviousPrice, AreaSqm, FirstSeen/LastSeenAt + pola AI: AiScore (0–100), AiSummary, AiProsJson, AiConsJson, AiPriceAssessment, EvaluatedAt.
  - `ScanRun` — Status (Running/Completed/Failed), liczniki, ReportHtml, Error.
  - `ScrapedListing` (rekord — wynik scrapera), `ListingEvaluation` (DTO do `ChatAsync<T>`), `SearchProfileRequest` (DTO API).
- **`Configuration/HomeSeekerOptions.cs`** ✅: Enabled (domyślnie **false**), ScanInterval (24h), StartupDelay (1 min), MaxSearchPagesPerPortal (5), RequestDelay (3s), MaxCandidatesPerScan (8 — limit kosztów), TopListingsInReport (5), Model (`claude-sonnet-5-5`), zagnieżdżone `Smtp` (`SmtpOptions`: Host, Port, Login, Password, FromAddress; `ToString()` maskuje hasło). Walidacja `IValidatableObject` + DataAnnotations, `ValidateOnStart()` (dodatni ScanInterval, nieujemne opóźnienia, port/FromAddress gdy ustawiony Host).
- **`Scrapers/`** ✅:
  - `IListingScraper` — `SearchAsync(SearchProfile, ct)` zwraca `ScrapeResult { Listings, Succeeded, Error }`; implementacje nie rzucają wyjątków — błąd HTTP/403/parsowania przed pobraniem pierwszej strony zwraca `Succeeded=false`. `MarketScanService` oznacza skan jako `Failed` (bez stempla `LastScannedAt`, ponowna próba w następnym cyklu), gdy żaden portal nie zakończył się sukcesem.
  - `OtodomScraper` — URL `otodom.pl/pl/wyniki/sprzedaz/dom/{miasto}?priceMin=..&areaMin=..&page=n`; parsowanie bloku JSON `<script id="__NEXT_DATA__">` (stabilniejsze niż selektory CSS); logika w statycznym `ParseNextData(html)` pod testy fixture.
  - `OlxScraper` — URL `olx.pl/nieruchomosci/domy/sprzedaz/{miasto}/?search[filter_float_price:from]=..`; główne źródło: `window.__PRERENDERED_STATE__` (regex + JSON); fallback: HtmlAgilityPack `[data-cy="l-card"]`.
  - Oba: nazwany HttpClient `"homeseeker-scraper"` (nagłówki przeglądarkowe, `Accept-Language: pl-PL`), odstęp między stronami, defensywne parsowanie (`TryGetProperty`), osobny warning przy 403.

### Faza 2 — persystencja (osobny DbContext, ta sama baza) ✅

**Pułapka `EnsureCreated()`**: to no-op, gdy baza ma już jakiekolwiek tabele — tabele HomeSeeker
nigdy nie powstałyby w istniejącej bazie Azure SQL. Rozwiązanie: osobny `HomeSeekerDbContext`
zawierający wyłącznie nowe encje oraz:

- **`ReceptyOks.Api/DbUtility/HomeSeekerDbContext.cs`** ✅ — DbSety SearchProfile/HouseListing/ScanRun; unikalny indeks `(SearchProfileId, Portal, ExternalId)`; indeksy UpdatedAt/IsDeleted/AiScore; kaskada profil→ogłoszenia/skany; precyzja decimali.
- **`ReceptyOks.Api/Extensions/HomeSeekerExtensions.cs`** ✅:
  - `AddHomeSeekerDatabase(...)` — kopia logiki `DatabaseExtensions.AddRecipeDatabase` (ten sam connection string, `AddDbContextPool`; SQLite dev / SQL Server z retry prod).
  - `EnsureHomeSeekerTablesCreated(...)` — `EnsureCreated()` (świeża baza), potem sonda `db.SearchProfiles.Any()`; przy braku tabeli `IRelationalDatabaseCreator.CreateTables()` — tylko tabele tego kontekstu.
  - `EnsureColumn(...)` — lekkie, idempotentne `ALTER TABLE ... ADD` dla kolumn dodanych po pierwszym wdrożeniu (`ScanLeaseUntil`, `ScanLeaseToken`, `OwnerId`); odporne na wyścig replik; inne błędy DDL przerywają start (fail-fast).
  - `AddHomeSeekerServices(...)` — opcje z walidacją, HttpClient `homeseeker-scraper`, scrapery (singleton), repo/ewaluator/scan service (scoped), fabryka agentów i sender (singleton), `AddHostedService<HomeSeekerScanService>`.
- **`HomeSeeker/Abstractions/IListingRepository.cs`** ✅ + **`ReceptyOks.Api/Repositories/ListingRepository.cs`** ✅: GetSharedProfileAsync / UpsertSharedProfileAsync (jeden wspólny profil — `OwnerId` = kto ostatnio edytował), GetProfileByIdAsync, TryAcquireScanLeaseAsync / ReleaseScanLeaseAsync (lease 30 min z tokenem; `markScanned` tylko przy sukcesie), Create/CompleteScanRunAsync, UpsertListingAsync (śledzenie zmian ceny; IsNew/PriceDropped), ResetTracking (po błędzie zapisu), SaveEvaluationAsync, GetTopListingsAsync, GetListingsAsync (stronicowanie/sort/minScore), GetScanHistoryAsync, GetScanRunByIdAsync.

### Faza 3 — ocena agentem i raport ✅

- **`HomeSeeker/Abstractions/IAiAgentFactory.cs`** + **`ReceptyOks.Api/Services/AnthropicAiAgentFactory.cs`** ✅: świeży `AiAgent` per wywołanie z `configuration["Token"]` + `AnthropicAgent` (model z `HomeSeeker:Model`, MaxTokens 4096). Parametr `withWebBrowsing` jest **ignorowany z ostrzeżeniem** — przeglądanie sieci wyłączone (C4).
- **`HomeSeeker/Evaluation/AgentListingEvaluator.cs`** ✅ (`IListingEvaluator`):
  - `EvaluateAsync(profile, listing, ct)`: agent bez narzędzi; polski system prompt ze ścisłym kontraktem JSON i ochroną przed prompt injection (dane ogłoszenia i `ExtraCriteria` w ogranicznikach „traktuj jako dane”); liczby w `InvariantCulture`; `ChatAsync<ListingEvaluation>(msg, maxToolRounds: 1)`. Normalizacja: `Pros/Cons ?? []`, `Score` przycinany do 0–100. **`null`/wyjątek → log + pominięcie, nigdy nie wywala skanu.**
  - `WriteReportHtmlAsync(...)`: **zawsze szablon w kodzie** (bez wywołania AI — eliminuje stored XSS przez treść ogłoszeń, C3): HTML-encoding, linki tylko dla bezpiecznych URL-i http(s), style inline, tabela rankingowa.
- Wybór kandydatów (w `MarketScanService`): tylko `IsNew || PriceDropped`, ranking po cenie/m², limit `MaxCandidatesPerScan`, ocena **sekwencyjna**.

### Faza 3.5 (opcjonalna) — swobodne przeszukiwanie sieci jako źródło ogłoszeń ⛔ wstrzymana

> **Zablokowana** decyzją bezpieczeństwa C4: `WebBrowsingTool` nie jest rejestrowany w agentach HomeSeeker.
> Realizacja wymaga najpierw bezpiecznego fetchera (allow-lista domen, walidacja DNS/IP przeciw SSRF,
> limit rozmiaru odpowiedzi) zamiast współdzielonego narzędzia. Poniższy projekt pozostaje jako referencja.

Agent jako dodatkowe *źródło odkrywania* ogłoszeń, obok deterministycznych scraperów —
łapie portale bez dedykowanego scrapera (Gratka, Domiporta, Morizon) kosztem dodatkowych tokenów per skan.

- **`HomeSeeker/Scrapers/AgentWebDiscoveryScraper.cs`** — implementuje `IListingScraper`
  (`PortalName = "WebDiscovery"`), więc wpina się w `MarketScanService` obok Otodom/OLX bez żadnych
  zmian w dedupach, ocenie i raporcie:
  - Wewnętrznie tworzy `AiAgent` przez `IAiAgentFactory` z zarejestrowanym `WebBrowsingTool`
    (`search_web` + `fetch_web_page`).
  - Prompt (polski, ścisły kontrakt JSON): „Znajdź aktualne ogłoszenia sprzedaży domów w {miasto}
    spełniające kryteria {cena, metraż}; szukaj na portalach innych niż Otodom i OLX;
    ZAWSZE zwróć wyłącznie JSON: {"listings": [{"portal", "externalId", "url", "title", "price", "areaSqm", "location"}]}".
  - `ChatAsync<WebDiscoveryResult>(prompt, maxToolRounds: 8)` → mapowanie na `ScrapedListing[]`;
    `externalId` = URL znormalizowany (host + ścieżka), gdy portal nie daje ID.
  - Walidacja wyników w kodzie (nie ufać agentowi): odrzucenie wpisów bez poprawnego URL,
    z ceną/metrażem poza kryteriami profilu; limit `WebDiscoveryMaxResults`.
  - `null`/zły JSON → log + pusta lista (fail-soft, jak pozostałe scrapery).
- **Konfiguracja**: `HomeSeeker:EnableWebDiscovery` (domyślnie **false**) — rejestracja warunkowa w DI;
  `HomeSeeker:WebDiscoveryMaxResults` (domyślnie 10).
- **Koszt**: jedno wywołanie agenta z ~8 rundami narzędzi per profil per skan — zauważalnie droższe
  niż scraper deterministyczny; dlatego wyłączone domyślnie i z osobnym limitem wyników.
- **Ograniczenie**: wyszukiwarki słabo indeksują świeże ogłoszenia — to uzupełnienie, nie zamiennik scraperów.
- **Testy**: fake `IAiAgentFactory` zwracający przygotowany JSON → mapowanie, walidacja odrzucająca
  śmieciowe wpisy, fail-soft przy `null`.

### Faza 4 — orkiestracja, e-mail, scheduler ✅

- **`HomeSeeker/Services/MarketScanService.cs`** ✅ (`IMarketScanService.RunScanAsync(profileId, ct)` → `bool`): przebieg wg diagramu w sekcji 2; try/catch per portal, per upsert (z `ResetTracking()`) i per ocena; wszystkie portale padły → `Failed`. Końcowy zapis `CompleteScanRunAsync` na **świeżym tokenie z 30 s timeoutem** (anulowanie nie zostawia skanu w `Running`). Zwraca `true` tylko gdy skan `Completed` i stan zapisany.
- **`HomeSeeker/Services/EmailScanReportSender.cs`** ✅ (`IScanReportSender.SendAsync(recipients, subject, html, ct)` → `bool`): nowy `MailSender` (MailKit) per wysyłka, `IsBodyHtml`; `NotificationEmails.Parse` → wielu odbiorców; brak `Smtp:Host` → warning + `false`; błędy SMTP logowane, nigdy nie rzuca. Mail wychodzi **tylko gdy skan znalazł nowe ogłoszenia lub obniżki**; pełny raport zawsze przez `GET /scans/{id}/report`.
- **`ReceptyOks.Shared/MessegeSender/MailSender.cs`** ✅ — przepisany na MailKit (rozwiązuje wcześniejsze błędy: stub `NotImplementedException`, dispose przed wysyłką, `Timeout = 20`).
- **`ReceptyOks.Api/Middleware/HomeSeekerScanService.cs`** ✅: `BackgroundService` (StartupDelay → skan od razu → sprawdzanie „czy należy się skan” co `min(ScanInterval, 5 min)`, scope per skan; dawniej tick co `ScanInterval` dawał efektywny cykl ~2×ScanInterval, bo `LastScannedAt` stemplowany jest na końcu skanu). Po nieudanym skanie przerwa `min(ScanInterval, 1 h)` przed ponowieniem. Harmonogram z konfiguracji; brak skanów na żądanie. Przy `Enabled=false` serwis kończy się od razu. Brak profilu / nieaktywny → pominięcie. Ochrona przed duplikatami między replikami: `TryAcquireScanLeaseAsync` → skan → `ReleaseScanLeaseAsync(markScanned: succeeded, CancellationToken.None)` w `finally`. Pętla nigdy nie faultuje hosta.

### Faza 5 — API ✅

**`ReceptyOks.Api/Endpoints/HomeSeekerEndpoints.cs`** ✅ — `MapGroup("/api/homeseeker")` + `"fixed"` rate limit + X-Api-Key + JWT (`RequireAuthorization`; claim `sub` przez `CallerIdentity.GetOwnerId`).

```
GET    /profile                     aktualne parametry skanu (404 dopóki nie ustawione)
PUT    /profile                     ustawienie parametrów na następny skan (FluentValidation; tworzy lub nadpisuje)
GET    /listings                    wyniki; stronicowane; ?minScore=&sort=score|price|firstSeen
GET    /scans                       historia skanów; ?count= (1–100, domyślnie 20)
GET    /scans/{id}/report           Results.Content(ReportHtml, "text/html") + restrykcyjne CSP/nosniff/X-Frame-Options/no-referrer
```

- `GET /listings` przyjmuje `pageNumber`/`pageSize` (max 100); bez profilu zwraca pustą stronę, `GET /scans` — pustą listę.
- **`Validators/SearchProfileRequestValidator.cs`** ✅: City wymagane ≤100; NotificationEmail = 1–5 adresów rozdzielonych `,`/`;` (każdy poprawny, łącznie ≤200 znaków; repo normalizuje do `a, b`); Max≥Min dla ceny/metrażu; ExtraCriteria ≤2000.
- **`Program.cs`** ✅: `AddHomeSeekerDatabase`, `AddHomeSeekerServices`, `EnsureHomeSeekerTablesCreated`, `MapHomeSeekerEndpoints`.

### Faza 6 — konfiguracja i wdrożenie — częściowo ✅

- ✅ `appsettings.json` (prod) / `.Development.json` — SMTP jest **zagnieżdżone** w sekcji `HomeSeeker`:

```json
// appsettings.json
"HomeSeeker": { "Enabled": false, "ScanInterval": "24:00:00", "StartupDelay": "00:01:00",
                "MaxSearchPagesPerPortal": 10, "RequestDelay": "00:00:03", "MaxCandidatesPerScan": 8,
                "TopListingsInReport": 5, "Model": "claude-sonnet-5-5",
                "Smtp": { "Host": "", "Port": 587, "Login": "", "FromAddress": "" } }
// appsettings.Development.json: ScanInterval 00:30:00, StartupDelay 0, 2 strony, 3 kandydatów, Model claude-haiku-4-5
```

- ✅ `.github/workflows/docker-build.yml` — ścieżka `HomeSeeker/**` w triggerach.
- ✅ Sekrety SMTP dodane ręcznie w Azure Key Vault (prod — `SecretsResolver` mapuje `--`→`:`, np. `HomeSeeker--Smtp--Password`); dev: user secrets.
- ✅ `ReceptyOks.Api/.azure/main.bicep` **celowo bez zmian** — konfiguracja HomeSeeker trafia do aplikacji przez Key Vault + `appsettings.json`, nie przez zmienne środowiskowe w Bicep.

### Faza 7 — testy i weryfikacja ✅ (automatyczne; ręczna weryfikacja przed włączeniem)

**Jednostkowe** (`ReceptyOks_UnitTests/HomeSeeker/`, NUnit + Moq, referencja HomeSeeker ✅):
- ✅ `OtodomScraperParserTests` (5), `OlxScraperParserTests` (12), `NotificationEmailsTests` (7) — parsery na fixture HTML/JSON, zepsute dane bez wyjątku.
- ✅ `ScraperSearchAsyncTests` (10) — stub `HttpMessageHandler`: stronicowanie i stop na pustej stronie, limit `MaxSearchPagesPerPortal`, budowa URL (normalizacja miasta, filtry), 403/500 na pierwszej stronie → `Succeeded=false`, błąd po pierwszej stronie → częściowy sukces, anulowanie bez wyjątku (Otodom + OLX).
- ✅ `MarketScanServiceTests` (12) — profil brak/nieaktywny, „wszystkie portale padły → Failed”, izolacja błędu portalu/upsertu (`ResetTracking`)/oceny, wybór kandydatów (nowe/obniżki, sort po cenie/m², `MaxCandidatesPerScan`), mail tylko przy nowych/obniżkach, błąd maila nie psuje skanu, anulowanie → `Failed` z nieanulowanym tokenem końcowego zapisu, nieudany końcowy zapis → `false`.
- ✅ `AgentListingEvaluatorTests` (15) — `Mock<IAiAgentFactory>`/`Mock<IAiAgent>`: `withWebBrowsing:false` + `maxToolRounds:1`, `null`/wyjątek → `null`, normalizacja `Pros/Cons`, clamp `Score`, ograniczniki danych w prompcie; raport bez AI, HTML-encoding, brak linków dla `javascript:`/`data:`/niepoprawnych URL-i.
- ✅ `HomeSeekerScanServiceTests` (9) — `Enabled=false` → brak wywołań, brak/nieaktywny profil, lease nieprzejęty → brak skanu, `markScanned` tylko przy sukcesie (porażka/wyjątek → `false`), błąd repozytorium nie wywraca serwisu, szybkie zatrzymanie w trakcie `StartupDelay`.

**Integracyjne** (`ReceptyOks.Api.IntegrationTests`) ✅: `CustomWebApplicationFactory` podmienia `HomeSeekerDbContext` na in-memory SQLite; `HomeSeekerEndpointsTests` (18) — profil GET/PUT, 400 z walidacji, stronicowanie, historia skanów, raport, wymóg autoryzacji.

**Ręczna weryfikacja:**
1. `dotnet build` + `dotnet test`.
2. Api w Development z `HomeSeeker:Enabled=true`, Scalar UI: `PUT /profile` (np. Wrocław, 400–900 tys. PLN, 90–200 m²), poczekać na skan (StartupDelay 0), logi, nowe tabele + ocenione rekordy w SQLite, `GET /scans/{id}/report` w przeglądarce.
3. Test maila przez smtp4dev/Mailtrap przed realnym SMTP.
4. Deploy z `HomeSeeker:Enabled=false`; weryfikacja utworzenia tabel w Azure SQL (`CreateTables()`); potem włączenie (`appsettings.json` lub sekret `HomeSeeker--Enabled` w Key Vault).

### Faza 8 — klient MAUI (aplikacja ReceptyOks) ✅

- **Wejście:** Ustawienia → „HomeSeeker – domy” (`HomeSeekerPage`, trasa w `AppShell`).
- **Auth:** ten sam przepływ co reszta aplikacji — `ApiKeyHandler` + `BearerTokenHandler` na typowanym `HttpClient` (`IHomeSeekerService` → `HomeSeekerService`). `AccessTokenRefresher` pobiera JWT przez `BackendAuthService.TryValidateAsync`, gdy tokenu brak. Po 401 odświeża token i ponawia żądanie raz.
- **DTO** (`Models/HomeSeeker/HomeSeekerModels.cs`): kopia kontraktu po stronie klienta, bo MAUI nie referencjonuje biblioteki HomeSeeker. Zgodność pilnują testy kontraktowe. `ScanStatus` jest liczbą.
- **Widok:** 3 sekcje.
  - Ogłoszenia: sort AI/cena/najnowsze, filtr min. oceny, stronicowanie przy przewijaniu, otwieranie tylko URL-i http(s).
  - Skany: historia z licznikami i błędem.
  - Profil: formularz `PUT /profile` z walidacją klienta i błędami `ValidationProblem` z serwera. Brak profilu (404) przełącza na tę sekcję.
- **Raport:** `HomeSeekerReportPage` = WebView z HTML z `/scans/{id}/report` i wstrzykniętym meta-CSP (`default-src 'none'`). Nawigacja http(s) w WebView jest anulowana, a link otwierany w przeglądarce systemowej.
- **Testy:** `HomeSeekerClientServiceTests` (24) — mapowanie statusów (404/400/401/błąd sieci), refresh + retry JWT, budowa query, zgodność DTO z modelami serwera, `ParseList`, `HasHttpUrl`, `ParseDecimal`, wstrzykiwanie CSP.

### Faza 9 — mapa ofert i „nasza lokalizacja” ✅ zaimplementowana (wizualnie do sprawdzenia w aplikacji)

**Cel:** zakładka „Mapa” w `HomeSeekerPage` z pinezkami ocenionych ofert (domyślnie top 50 wg `AiScore`)
oraz punktem „nasza lokalizacja”, który zastępuje centrum miasta przy liczeniu odległości i w ocenie AI.

**Decyzje:**
- Mapa: **Mapsui.Maui** (OpenStreetMap, bez kluczy; pakiet ma `net10.0-android`/`net10.0-windows`).
  `CommunityToolkit.Maui.Maps` odpada (tylko Windows + przestarzałe klucze Bing).
- Tylko ocenione oferty — każda przeszła przez stronę szczegółów, więc współrzędne są dostępne
  bez dodatkowego geokodowania (Otodom dokładne, OLX przybliżone).
- Mapa używa tych samych filtrów co lista (typ, min. ocena, portal, odległość, obniżka) + `limit` (domyślnie 50, max 200).
- Lokalizację ustawia się long-pressem na mapie lub przyciskiem GPS.
- Starsze ocenione oferty bez współrzędnych — jednorazowy, stopniowy backfill w tle.

**Backend:**
1. **Model + schemat** (`EnsureColumn` w `HomeSeekerExtensions`):
   - `HouseListing`: `Latitude`, `Longitude` (`double?`), `CoordinatesApproximate` (`bool`).
   - `SearchProfile`: `HomeLatitude`, `HomeLongitude` (`double?`), `HomeLabel` (`string?`);
     `SearchProfileRequest` + walidator (zakresy lat/lon, oba albo żaden).
2. **Zapis współrzędnych** w `MarketScanService.EvaluateCandidatesAsync` — nowa metoda repozytorium
   `SaveCoordinatesAsync`; dokładne nadpisują przybliżone, nigdy odwrotnie.
3. **Punkt odniesienia:** `profile.Home ?? cityCenter` w `UpdateDistancesAsync`/`EnrichBenchmarkAsync`;
   prompt AI mówi o odległości „od wskazanej lokalizacji” zamiast „od centrum”, gdy Home ustawiony.
   Zmiana Home w `PUT /profile` przelicza `DistanceFromCenterKm` dla ofert z zapisanymi współrzędnymi (bez ponownej oceny AI).
4. **Backfill:** ocenione oferty bez współrzędnych, partiami, z `RequestDelay`; pomijany, gdy trwa lease skanu.
5. **Endpoint** `GET /map?limit=50&minScore=&propertyType=&portal=&maxDistanceKm=&priceDropOnly=` →
   lekkie DTO (Id, Title, Url, Price, AreaSqm, AiScore, Lat, Lon, Approx, Distance, PropertyType, Portal);
   tylko oceniane z współrzędnymi, z limitem na inwestycję jak w liście (`ListingGroups.CapPerGroup`).

**MAUI:**
1. `Mapsui.Maui` + inicjalizacja w `MauiProgram`; nowy `TabItem "Mapa"` z `MapControl` i osobnym
   `HomeSeekerMapViewModel` (ładowanie przy pierwszym wejściu w zakładkę, odświeżanie po zmianie filtrów).
2. Warstwa OSM z poprawnym User-Agent (zgodnie z tile usage policy) i atrybucją.
3. Pinezki: kolor wg oceny (≥75 zielony, 50–74 żółty, <50 szary); lokalizacje przybliżone jako półprzezroczyste kółko.
4. **Rozsuwanie nakładających się pinezek:** oferty o (prawie) tych samych współrzędnych (~10 m) są grupowane
   i rozkładane po okręgu wokół wspólnego punktu (offset w pikselach, `SymbolStyle.Offset`), więc rozsunięcie
   działa przy każdym zoomie; środek grupy oznaczony kropką. Algorytm jako czysta, testowalna funkcja.
5. Tap w pinezkę → karta na dole (tytuł, cena, m², ocena, odległość, „Otwórz ofertę”).
6. Nasza lokalizacja: long-press → potwierdzenie → `PUT /profile`; przycisk GPS (`Geolocation`,
   uprawnienie lokalizacji w manifeście Android); wyróżniona pinezka domu.
7. `HomeSeekerService`: `GetMapListingsAsync`, profil z polami Home; DTO w `HomeSeekerModels.cs` + testy kontraktowe.

**Testy:** zapis współrzędnych (dokładne > przybliżone), wybór punktu odniesienia Home vs centrum,
przeliczenie odległości po zmianie Home, endpoint mapy (filtry, limit, tylko z współrzędnymi),
walidator Home, backfill (pomija przy lease / bez scrapera), algorytm rozsuwania pinezek, DTO klienta.

**Dodatkowe użycie `GeoPoint`:**
- Kontrola wiarygodności: współrzędne dalej niż 60 km od centrum miasta są odrzucane (błędny pin na portalu).
- Deduplikacja po lokalizacji w `ListingGroups`: dokładne piny ≤ 30 m, ten sam typ, metraż ±2%, cena ±5%
  łączą kopie z różnych portali, których nie złapał dokładny klucz `dup:` (przybliżone piny OLX pomijane).

**Realizacja:** mapa w `ReceptyOks/Views/HomeSeekerMapView.cs` (Mapsui 5.1, budowana leniwie w code-behind),
stan w `HomeSeekerViewModel.Map.cs` (zamiast osobnego VM); rozsuwanie w `Models/HomeSeeker/MapPinLayout.cs`.

## 5. Model danych

```
SearchProfile 1 ──── * HouseListing      (unikalny: SearchProfileId+Portal+ExternalId)
       1 ──── * ScanRun                  (historia skanów + ReportHtml)
```

## 6. Kontrola kosztów AI

- Ocena tylko ogłoszeń nowych lub z obniżką ceny; limit `MaxCandidatesPerScan` (domyślnie 8).
- Preselekcja deterministyczna (cena/m²) przed agentem — bez tokenów.
- Tańszy model do oceny (`HomeSeeker:Model`; dev: Haiku), ocena bez narzędzi (`maxToolRounds: 1`); raport bez AI (szablon) — zero tokenów.
- Ocena sekwencyjna — szanuje limity API i 0.25 CPU kontenera.
- Swobodne przeszukiwanie sieci (Faza 3.5) domyślnie wyłączone — najdroższy element, włączany świadomie
  flagą `EnableWebDiscovery` z własnym limitem `WebDiscoveryMaxResults`.

## 7. Ryzyka i mitygacje

1. **Blokady anty-botowe portali (403/captcha)**: przeglądarkowe nagłówki, odstępy między żądaniami, łagodna degradacja (`ScrapeResult.Failure` zamiast wyjątku; skan `Failed` tylko gdy padną wszystkie portale), osobny warning przy 403.
2. **Dryf struktur JSON** (`__NEXT_DATA__`/`__PRERENDERED_STATE__`): defensywne parsowanie + testy fixture + fail-soft.
3. **Ewolucja schematu bez migracji**: pierwszy rollout przez `CreateTables()`; nowe kolumny przez `EnsureColumn` w `HomeSeekerExtensions` (każda kolejna zmiana = nowy wpis tam).
4. **Wiele replik / równoległe skany**: lease z tokenem (`TryAcquireScanLeaseAsync`, 30 min) — tylko jedna instancja skanuje profil; skan dłuższy niż lease może zostać przejęty (warning).
5. ~~Błędy `MailSender`~~ — ✅ rozwiązane migracją na MailKit.
6. **`ChatAsync<T>` zwraca `null` przy złym JSON**: ewaluator traktuje jako pominięcie, skan zawsze kończy się raportem.
7. **Prompt injection / XSS przez treść ogłoszeń**: brak narzędzi w agencie (C4), dane w ogranicznikach promptu, raport z szablonu z HTML-encoding (C3), restrykcyjne CSP na `/scans/{id}/report`.
8. **Skan zawieszony w `Running`** gdy baza niedostępna przy końcowym zapisie: logowane; zalecany job rekoncyliacyjny przy starcie (do zrobienia).

## 8. Status implementacji

| Faza | Status |
|---|---|
| 1. Biblioteka (modele, opcje, scrapery) | ✅ zaimplementowana |
| 2. Persystencja | ✅ `HomeSeekerDbContext`, `HomeSeekerExtensions` (+ `EnsureColumn`), `ListingRepository` z lease |
| 3. Ocena agentem | ✅ bez web browsingu (C4), raport z szablonu (C3) |
| 3.5. Swobodne przeszukiwanie sieci (opcjonalna) | ⛔ wstrzymana (wymaga bezpiecznego fetchera) |
| 4. Orkiestracja + e-mail + scheduler | ✅ `MarketScanService`, `EmailScanReportSender` (MailKit), `HomeSeekerScanService` |
| 5. API | ✅ `/profile`, `/listings`, `/scans`, `/scans/{id}/report` + walidator |
| 6. Konfiguracja | ✅ appsettings, CI, sekrety SMTP w Key Vault; `main.bicep` celowo bez zmian |
| 7. Testy | ✅ 70 jednostkowych HomeSeeker (parsery, `NotificationEmails`, `SearchAsync`, `MarketScanService`, `AgentListingEvaluator`, `HomeSeekerScanService`) + 18 integracyjnych endpointów |
| 8. Klient MAUI | ✅ `HomeSeekerPage` + `HomeSeekerReportPage`, JWT refresh/retry, 24 testy klienta |
| 9. Mapa ofert + nasza lokalizacja | ✅ (Mapsui, współrzędne, `/map`, backfill, Home, dedup po lokalizacji) |

### Pozostało do zrobienia

1. Job rekoncyliacyjny oznaczający przeterminowane `Running` jako `Failed`.
2. Ręczna weryfikacja (Faza 7, kroki 2–3), potem włączenie na produkcji przez `HomeSeeker:Enabled=true` w `appsettings.json` przed deployem (feature-flag / reload z Key Vault — ewentualnie później; awaryjny wyłącznik w runtime: `IsActive=false` przez `PUT /profile`).
