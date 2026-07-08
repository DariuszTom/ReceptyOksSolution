# HomeSeeker — AI Real-Estate Market Scanner

Plan implementacji i projekt architektury agenta AI skanującego rynek nieruchomości
w wybranym mieście (portale polskie: **Otodom**, **OLX**) w poszukiwaniu dobrych domów.

## 1. Cel i założenia

| Decyzja | Wybór |
|---|---|
| Architektura | `HomeSeeker` = biblioteka klas z całą logiką domenową; `ReceptyOks.Api` (już wdrożone na Azure Container Apps) referencuje ją, hostuje cykliczne skany i wystawia endpointy — reużycie Key Vault, bazy, autoryzacji i pipeline'u wdrożeniowego |
| Pozyskiwanie danych | **Hybryda**: deterministyczne scrapery per portal zbierają tanio kandydatów; istniejący `AiAgent` + `WebBrowsingTool` pobiera strony szczegółów najlepszych kandydatów i je ocenia |
| Wyzwalanie | Użytkownik tworzy **profil wyszukiwania** przez API (miasto, metraż min/max, przedział cenowy, kryteria wolnym tekstem); `BackgroundService` cyklicznie skanuje każdy aktywny profil; dodatkowo skan na żądanie |
| Wynik | Ocenione ogłoszenia w bazie (deduplikacja między skanami) + **raport HTML pisany przez AI wysyłany mailem** po każdym skanie |

## 2. Architektura

```
┌────────────────────────────────────────────────────────────────────────────┐
│ ReceptyOks.Api (Azure Container App)                                       │
│                                                                            │
│  HomeSeekerEndpoints          HomeSeekerScanService (BackgroundService)    │
│  /api/homeseeker/*                 │ PeriodicTimer + ScanTriggerQueue      │
│        │                           ▼                                       │
│        │                    IMarketScanService ◄──────────────┐            │
│        ▼                           │                          │            │
│  SearchProfileRequestValidator     │ orkiestracja skanu       │            │
│  (FluentValidation)                ▼                          │            │
│                     ┌──────────────┴───────────────┐          │            │
│                     ▼                              ▼          │            │
│              IListingScraper[]              IListingEvaluator │            │
│              (Otodom, OLX)                  (AiAgent + WebBrowsingTool)    │
│                     │                              │          │            │
│                     ▼                              ▼          ▼            │
│              IListingRepository ──── HomeSeekerDbContext ── IScanReport-   │
│              (upsert + dedup)        (SQLite dev/Azure SQL) Sender (SMTP)  │
└────────────────────────────────────────────────────────────────────────────┘
         HomeSeeker (biblioteka): modele, scrapery, ewaluacja, orkiestracja
         ReceptyOks.Shared: AiAgent, AnthropicAgent, WebBrowsingTool, MailSender
```

**Kierunek zależności:** `HomeSeeker` → `ReceptyOks.Shared`; `ReceptyOks.Api` → `HomeSeeker`.
EF Core, hosting i endpointy zostają w Api (spójnie z `RecipeDbContext` w `ReceptyOks.Api/DbUtility`);
HomeSeeker trzyma logikę domenową za interfejsami (`Abstractions/`), więc jest wolny od EF i testowalny jednostkowo.

**Przepływ pojedynczego skanu:**

```
ScanRun(Running) → scrapery (try/catch per portal) → upsert/dedup po (profil, portal, externalId)
  → wybór kandydatów: tylko nowe lub z obniżką ceny, ranking po cenie/m², limit MaxCandidatesPerScan
  → ocena sekwencyjna agentem (fetch strony szczegółów + JSON: score, pros, cons, priceAssessment)
  → raport HTML pisany przez agenta (fallback: tabela generowana w kodzie)
  → e-mail do NotificationEmail → ScanRun(Completed, liczniki, ReportHtml)
```

## 3. Infrastruktura do reużycia (zweryfikowana w kodzie)

- `ReceptyOks.Shared/AI/AiAgent.cs` — `ChatAsync<T>` (strukturalny JSON), `AddTool*`; **stanowy (sesja per instancja) → tworzenie per ocena przez fabrykę**.
- `ReceptyOks.Shared/AI/AnthropicAgent.cs` + `AnthropicSettings.cs` — `IChatClient` z Anthropic SDK; klucz API dostępny po stronie serwera jako `configuration["Token"]` (Key Vault przez `SecretsResolver`, patrz `TokenProviderEndpoints.cs:48`).
- `ReceptyOks.Shared/AI/WebBrowsingTool.cs` — `RegisterTools(agent)` dodaje `fetch_web_page` i `search_web`.
- `ReceptyOks.Shared/MessegeSender/MailSender.cs` — wysyłka SMTP (wymaga 2 poprawek błędów, sekcja 7).
- Wzorce w `ReceptyOks.Api`: grupy endpointów (`Endpoints/RecipeEndpoints.cs`), `BackgroundService` + `PeriodicTimer` (`Middleware/ShoppingListCleaner.cs`), poolowany DbContext SQLite dev / Azure SQL prod (`Extensions/DatabaseExtensions.cs`), FluentValidation, rate limitery `"fixed"`/`"strict"`, auth X-Api-Key działa automatycznie.
- `AddServiceDefaults()` już dodaje standardową odporność (retry/timeout/circuit-breaker) do wszystkich klientów `IHttpClientFactory` — bez dodatkowego Polly.

## 4. Fazy implementacji

### Faza 1 — biblioteka HomeSeeker ✅ (zaimplementowana)

- **`HomeSeeker.csproj`** ✅: referencja do `ReceptyOks.Shared`; pakiety `HtmlAgilityPack`, `Microsoft.Extensions.Options/Http/Logging.Abstractions`.
- **`Models/`** ✅ (wzorzec soft-delete `IsDeleted` + `UpdatedAt` jak `Recipe.cs`):
  - `SearchProfile` — kryteria: City, District?, Min/MaxPrice, Min/MaxAreaSqm, ExtraCriteria (wolny tekst dla agenta), NotificationEmail, IsActive, LastScannedAt.
  - `HouseListing` — jednostka deduplikacji: Portal, ExternalId, Url, Price, PreviousPrice, AreaSqm, FirstSeen/LastSeenAt + pola AI: AiScore (0–100), AiSummary, AiProsJson, AiConsJson, AiPriceAssessment, EvaluatedAt.
  - `ScanRun` — Status (Running/Completed/Failed), liczniki, ReportHtml, Error.
  - `ScrapedListing` (rekord — wynik scrapera), `ListingEvaluation` (DTO do `ChatAsync<T>`), `SearchProfileRequest` (DTO API).
- **`Configuration/HomeSeekerOptions.cs`** ✅: Enabled (domyślnie **false**), ScanInterval (12h), StartupDelay, MaxSearchPagesPerPortal (2), RequestDelay (3s), MaxCandidatesPerScan (8 — limit kosztów), TopListingsInReport (5), Model (tańszy model do oceny). `SmtpOptions`: Host, Port, Login, Password, FromAddress.
- **`Scrapers/`** ✅:
  - `IListingScraper` — `SearchAsync(SearchProfile, ct)`; implementacje nie rzucają wyjątków dla problemów z treścią.
  - `OtodomScraper` — URL `otodom.pl/pl/wyniki/sprzedaz/dom/{miasto}?priceMin=..&areaMin=..&page=n`; parsowanie bloku JSON `<script id="__NEXT_DATA__">` (stabilniejsze niż selektory CSS); logika w statycznym `ParseNextData(html)` pod testy fixture.
  - `OlxScraper` — URL `olx.pl/nieruchomosci/domy/sprzedaz/{miasto}/?search[filter_float_price:from]=..`; główne źródło: `window.__PRERENDERED_STATE__` (regex + JSON); fallback: HtmlAgilityPack `[data-cy="l-card"]`.
  - Oba: nazwany HttpClient `"homeseeker-scraper"` (nagłówki przeglądarkowe, `Accept-Language: pl-PL`), odstęp między stronami, defensywne parsowanie (`TryGetProperty`), osobny warning przy 403.

### Faza 2 — persystencja (osobny DbContext, ta sama baza) — częściowo ✅

**Pułapka `EnsureCreated()`**: to no-op, gdy baza ma już jakiekolwiek tabele — tabele HomeSeeker
nigdy nie powstałyby w istniejącej bazie Azure SQL. Rozwiązanie: osobny `HomeSeekerDbContext`
zawierający wyłącznie nowe encje oraz:

- **`ReceptyOks.Api/DbUtility/HomeSeekerDbContext.cs`** ✅ — DbSety SearchProfile/HouseListing/ScanRun; unikalny indeks `(SearchProfileId, Portal, ExternalId)`; indeksy UpdatedAt/IsDeleted/AiScore; kaskada profil→ogłoszenia/skany; precyzja decimali.
- **`ReceptyOks.Api/Extensions/HomeSeekerExtensions.cs`** (do zrobienia):
  - `AddHomeSeekerDatabase(...)` — kopia logiki `DatabaseExtensions.AddRecipeDatabase` (ten sam connection string, `AddDbContextPool`).
  - `EnsureHomeSeekerTablesCreated(...)` — `EnsureCreated()` (świeża baza), potem sonda `db.SearchProfiles.Any()`; przy braku tabeli `GetService<IRelationalDatabaseCreator>().CreateTables()` — tworzy tylko tabele tego kontekstu, bez kolizji z tabelami przepisów. **Uwaga w kodzie:** przyszłe zmiany *kolumn* nadal wymagają ręcznego SQL (projekt nie używa migracji).
- **`HomeSeeker/Abstractions/IListingRepository.cs`** ✅ (implementacja `ReceptyOks.Api/Repositories/ListingRepository.cs` do zrobienia): GetActiveProfilesAsync, TryMarkProfileScannedAsync (blokada duplikatów między replikami), Create/CompleteScanRunAsync, UpsertListingAsync (śledzenie zmian ceny; flagi IsNew/PriceDropped), SaveEvaluationAsync, GetTopListingsAsync.

### Faza 3 — ocena agentem i raport

- **`HomeSeeker/Abstractions/IAiAgentFactory.cs`** + **`ReceptyOks.Api/Services/AnthropicAiAgentFactory.cs`**: świeży `AiAgent` per wywołanie z `configuration["Token"]` + `AnthropicAgent`; przy `withWebBrowsing` rejestruje `WebBrowsingTool` z klientem scraperów. Tańszy model z konfiguracji `HomeSeeker:Model` (nie `claude-opus-4-7`).
- **`HomeSeeker/Evaluation/AgentListingEvaluator.cs`** (`IListingEvaluator`):
  - `EvaluateAsync(profile, listing, ct)`: agent z WebBrowsingTool; polski system prompt ze ścisłym kontraktem JSON (wzór: `AnthropicSettings.SystemPromtShoppingList`); `ChatAsync<ListingEvaluation>(msg, maxToolRounds: 4)`. **`null` (zły JSON) → log + pominięcie, nigdy nie wywala skanu.**
  - Wybór kandydatów: tylko `IsNew || PriceDropped`, ranking po cenie/m², limit `MaxCandidatesPerScan`, ocena **sekwencyjna** (kontener 0.25 CPU, limity API).
  - `WriteReportHtmlAsync(...)`: jedno wywołanie agenta bez narzędzi → samodzielny HTML (style inline, polski, tabela rankingowa, linki). Fallback: tabela budowana w kodzie, żeby mail zawsze wyszedł.

### Faza 4 — orkiestracja, e-mail, scheduler

- **`HomeSeeker/Services/MarketScanService.cs`** (`IMarketScanService.RunScanAsync(profileId, ct)`): przebieg wg diagramu w sekcji 2; try/catch per portal i per ogłoszenie; błąd całości → ScanRun(Failed, Error).
- **`HomeSeeker/Services/EmailScanReportSender.cs`** (`IScanReportSender.SendAsync(recipient, subject, html, ct)`): opakowuje `MailSender` (IsBodyHtml, konfiguracja ze `SmtpOptions`, dispose per wysyłka).
- **Poprawki `ReceptyOks.Shared/MessegeSender/MailSender.cs`** (realne błędy, zweryfikowane):
  1. Jawna implementacja `IMailSender.MailConfig` (linia 71) rzuca `NotImplementedException` — ujednolicić interfejs do `Task<bool>` i usunąć stub.
  2. `SendMail` zwraca task wysyłki, a `using (_mailMessage)` dispose'uje wiadomość przed zakończeniem wysyłki → `await` wewnątrz using; zwracać `Task`.
  3. `Timeout = 20` → `20_000` (kosmetyka; dotyczy wysyłki synchronicznej).
- **`ReceptyOks.Api/Services/ScanTriggerQueue.cs`**: singleton z `Channel<Guid>` dla skanów na żądanie.
- **`ReceptyOks.Api/Middleware/HomeSeekerScanService.cs`**: `BackgroundService` wg `ShoppingListCleaner` (PeriodicTimer, scope per skan, try/catch per profil). Pętla czeka na tick timera LUB odczyt z kanału; tick skanuje aktywne profile z przeterminowanym `LastScannedAt`. Przy `Enabled=false` obsługuje tylko kanał na żądanie. Ochrona przed duplikatami przy 2 replikach: `TryMarkProfileScannedAsync` (update-first); rozważyć maxReplicas=1.

### Faza 5 — API

**`ReceptyOks.Api/Endpoints/HomeSeekerEndpoints.cs`** — `MapGroup("/api/homeseeker")` + `"fixed"` rate limit:

```
POST   /profiles                    utworzenie (FluentValidation)
GET    /profiles                    lista (bez IsDeleted)
GET    /profiles/{id}               pojedynczy
PUT    /profiles/{id}               aktualizacja
DELETE /profiles/{id}               soft delete
POST   /profiles/{id}/scan          limiter "strict" → ScanTriggerQueue → 202 Accepted
GET    /profiles/{id}/listings      stronicowane; ?minScore=&sort=score|price|firstSeen
GET    /profiles/{id}/scans         historia (bez ReportHtml)
GET    /scans/{id}/report           Results.Content(ReportHtml, "text/html")
```

- **`Validators/SearchProfileRequestValidator.cs`**: City wymagane ≤100; NotificationEmail poprawny; Max≥Min dla ceny/metrażu; ExtraCriteria ≤2000.
- **`Program.cs`**: `Configure<HomeSeekerOptions>/<SmtpOptions>`, `AddHomeSeekerDatabase`, nazwany HttpClient `"homeseeker-scraper"`, DI (scrapery/repo/fabryka/ewaluator/sender/scan service), `AddSingleton<ScanTriggerQueue>`, `AddHostedService<HomeSeekerScanService>`, health check `homeseeker-db`; po `app.EnsureDatabaseCreated()` → `app.EnsureHomeSeekerTablesCreated()`; `app.MapHomeSeekerEndpoints()`. Referencja HomeSeeker w `ReceptyOks.Api.csproj`.

### Faza 6 — konfiguracja i wdrożenie

- `appsettings.json` / `.Development.json`:

```json
"HomeSeeker": { "Enabled": false, "ScanInterval": "12:00:00", "MaxCandidatesPerScan": 8,
                "MaxSearchPagesPerPortal": 2, "RequestDelay": "00:00:03", "Model": "claude-sonnet-4-6" },
"Smtp": { "Host": "", "Port": 587, "Login": "", "FromAddress": "" }
```

- Hasło SMTP: user secrets (dev) / Key Vault `Smtp--Password` (prod — `SecretsResolver` mapuje `--`→`:`).
- `.azure/main.bicep`: secret `smtp-password`; zmienne `HomeSeeker__Enabled`, `HomeSeeker__ScanInterval`, `Smtp__*` (wzorzec `Jwt__Key`). Dodanie sekretu do Key Vault = krok ręczny.

### Faza 7 — testy i weryfikacja

**Jednostkowe** (`ReceptyOks_UnitTests`, NUnit + Moq, dodać referencję HomeSeeker):
- Scrapery: fixture HTML (zasoby osadzone) → asercje pól `ScrapedListing`; zepsuty HTML → pusta lista bez wyjątku; jeden test `SearchAsync` ze stubowanym `HttpMessageHandler` (wzorzec w `WebBrowsingToolTests.cs`).
- `MarketScanServiceTests`: mocki — wybór kandydatów, limit kosztów, izolacja błędów per portal/ogłoszenie, przejścia statusów ScanRun, wysyłka maila.
- `AgentListingEvaluatorTests`: `Mock<IChatClient>` z gotowym JSON (wzorzec `AiAgentTests.cs`); obsługa `null` przy złym JSON.

**Integracyjne** (`ReceptyOks.Api.IntegrationTests`): rozszerzyć `CustomWebApplicationFactory` (podmiana `HomeSeekerDbContext` jak `RecipeDbContext`, `HomeSeeker:Enabled=false`); `HomeSeekerEndpointsTests`: CRUD profili, 400 z walidacji, 202 przy skanie, stronicowanie, wymóg autoryzacji.

**Ręczna weryfikacja:**
1. `dotnet build` + `dotnet test`.
2. Api w Development, Scalar UI: POST profil (np. Wrocław, 400–900 tys. PLN, 90–200 m²), POST `/scan`, logi, nowe tabele + ocenione rekordy w SQLite, GET raportu w przeglądarce.
3. Test maila przez smtp4dev/Mailtrap przed realnym SMTP.
4. Deploy z `HomeSeeker__Enabled=false`; weryfikacja utworzenia tabel w Azure SQL (`CreateTables()`); potem włączenie.

## 5. Model danych

```
SearchProfile 1 ──── * HouseListing      (unikalny: SearchProfileId+Portal+ExternalId)
       1 ──── * ScanRun                  (historia skanów + ReportHtml)
```

## 6. Kontrola kosztów AI

- Ocena tylko ogłoszeń nowych lub z obniżką ceny; limit `MaxCandidatesPerScan` (domyślnie 8).
- Preselekcja deterministyczna (cena/m²) przed agentem — bez tokenów.
- Tańszy model do oceny (`HomeSeeker:Model`), pojedyncze wywołanie bez narzędzi dla raportu.
- Ocena sekwencyjna — szanuje limity API i 0.25 CPU kontenera.

## 7. Ryzyka i mitygacje

1. **Wykrywanie botów / ToS**: Otodom (Cloudflare) i OLX mogą zwracać 403 dla IP Azure — realistyczne nagłówki, niska częstotliwość, łagodna degradacja (pusty wynik), osobny warning przy 403. Scraping narusza regulaminy portali — akceptowalne dla narzędzia osobistego; abstrakcja `IListingScraper` pozwala później podmienić źródło na API/RSS.
2. **Dryf struktur JSON** (`__NEXT_DATA__`/`__PRERENDERED_STATE__`): defensywne parsowanie + testy fixture + fail-soft.
3. **Ewolucja schematu bez migracji**: pierwszy rollout przez `CreateTables()`; późniejsze zmiany kolumn = ręczny SQL (udokumentowane w kodzie).
4. **2 repliki → podwójne skany/maile**: guard `TryMarkProfileScannedAsync`; rozważyć maxReplicas=1.
5. **Błędy `MailSender`**: bez poprawek (sekcja Faza 4) wysyłka będzie się losowo wywalać `ObjectDisposedException`.
6. **`ChatAsync<T>` zwraca `null` przy złym JSON**: ewaluator traktuje jako pominięcie, skan zawsze się kończy raportem.

## 8. Status implementacji

| Faza | Status |
|---|---|
| 1. Biblioteka (modele, opcje, scrapery) | ✅ zaimplementowana, build zielony |
| 2. Persystencja | częściowo — `HomeSeekerDbContext` ✅, `IListingRepository` ✅; brakuje `HomeSeekerExtensions` i `ListingRepository` |
| 3. Ocena agentem | do zrobienia |
| 4. Orkiestracja + e-mail + scheduler | do zrobienia |
| 5. API | do zrobienia |
| 6. Konfiguracja + Bicep | do zrobienia |
| 7. Testy | do zrobienia |
