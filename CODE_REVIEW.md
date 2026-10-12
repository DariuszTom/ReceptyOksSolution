# HomeSeeker — Code Review & Recommendations

Review date: 2026-10-12. Branch: `HomeSeekerImplementation`. Scope: the `HomeSeeker/` class library, its hosting code in `ReceptyOks.Api/` (endpoints, background service, repository, DbContext, DI), the Shared AI/mail dependencies it uses, tests, CI workflows and deployment files.

This was a **static review**: nothing was built, run or deployed. Where a conclusion depends on runtime behaviour it is marked *verify*. Line numbers refer to the current state of the branch and will drift as files change.

## 1. Summary

HomeSeeker is functionally complete for plan phases 1–5 (library, persistence, AI evaluation, orchestration, API), but it is not shippable yet:

- **It cannot be deployed.** The Dockerfile never copies the `HomeSeeker` project, so the image build fails at restore (C1), and no configuration, secrets or Bicep settings exist for it (H8).
- **One bug can stop the whole API host.** The background scan loop misuses `PeriodicTimer` and will throw after the first on-demand scan (C2).
- **Two security issues.** AI-generated HTML is served and emailed unsanitized (stored XSS, C3), and the evaluation agent has an unrestricted web-fetch tool fed with scraped, attacker-controlled text (SSRF + prompt injection, C4).
- **Several correctness bugs** break the core loop in realistic conditions: scans stuck in `Running` on shutdown (H1), on-demand scans silently skipped (H2), one failed insert poisoning the rest of the scan (H3), unstable dedup keys (H4).
- **No tests and no CI coverage** for any HomeSeeker code (H10).

| Severity | Count | Meaning |
|---|---|---|
| Critical | 5 | Blocks deployment, crashes the host, or is a security hole |
| High | 11 | Breaks core functionality or data integrity in normal use |
| Medium | 19 | Robustness, cost, data quality, API hygiene |
| Low | 9 | Maintainability, consistency, documentation |

Section 7 gives a suggested order of attack.

## 2. Scope & method

Files reviewed in full:

- `HomeSeeker/` — `Abstractions/*`, `Configuration/HomeSeekerOptions.cs`, `Evaluation/AgentListingEvaluator.cs`, `Models/*`, `Scrapers/OlxScraper.cs`, `Scrapers/OtodomScraper.cs`, `Services/MarketScanService.cs`, `Services/EmailScanReportSender.cs`, `HomeSeeker.csproj`, `IMPLEMENTATION_PLAN.md`
- `ReceptyOks.Api/` — `Endpoints/HomeSeekerEndpoints.cs`, `Middleware/HomeSeekerScanService.cs`, `Repositories/ListingRepository.cs`, `DbUtility/HomeSeekerDbContext.cs`, `Extensions/HomeSeekerExtensions.cs`, `Services/AnthropicAiAgentFactory.cs`, `Services/ScanTriggerQueue.cs`, `Validators/SearchProfileRequestValidator.cs`, HomeSeeker parts of `Program.cs`, `appsettings*.json`, `Dockerfile`, `.azure/main.bicep`
- `ReceptyOks.Shared/` — `AI/AiAgent.cs`, `AI/WebBrowsingTool.cs`, `MessegeSender/MailSender.cs`
- `ReceptyOksSolution.ServiceDefaults/Extensions.cs`, both test projects, `.github/workflows/*.yml`

Severity legend:

- **Critical** — must fix before the feature is deployed or enabled.
- **High** — fix before relying on the feature; breaks core behaviour or data.
- **Medium** — should fix; affects robustness, cost, quality or API consistency.
- **Low** — nice to have; maintainability and documentation.

## 3. Findings

### Critical

#### C1. Docker image cannot build

- `ReceptyOks.Api/Dockerfile:6-8` and `:14-16` copy only `ReceptyOks.Api`, `ReceptyOks.Shared` and `ReceptyOksSolution.ServiceDefaults`.
- `ReceptyOks.Api/ReceptyOks.Api.csproj:39` references `..\HomeSeeker\HomeSeeker.csproj`, so `dotnet restore` at `Dockerfile:11` fails inside the container.
- `.github/workflows/docker-build.yml:30-40` path filters cover only the three old projects, so changes under `HomeSeeker/**` do not trigger an image build.

**Fix:** add `COPY HomeSeeker/HomeSeeker.csproj HomeSeeker/` before the restore and `COPY HomeSeeker/ HomeSeeker/` with the other sources; add `HomeSeeker/**` to both path filter lists. *Verify* with `docker build -f ReceptyOks.Api/Dockerfile .`

#### C2. Background scan loop can stop the whole host

`ReceptyOks.Api/Middleware/HomeSeekerScanService.cs:54-76`:

- Each iteration calls `timer.WaitForNextTickAsync(...)` again (`:58`). When the loop is woken by the on-demand channel, the previous timer wait is still pending. `PeriodicTimer` allows only one concurrent waiter and throws `InvalidOperationException`.
- Only `OperationCanceledException` is caught (`:78`). The unhandled exception faults `ExecuteAsync`; with the default `BackgroundServiceExceptionBehavior.StopHost`, **the API process stops**.
- `:72` `task is Task<bool> timerTask` is true for both awaited tasks, because `WaitToReadAsync().AsTask()` is also a `Task<bool>`. So every on-demand trigger also runs a full `ScanActiveProfilesAsync`.
- A second unprotected path: `new PeriodicTimer(_options.ScanInterval)` (`:50`) throws for a non-positive interval (see H9).

**Fix:** keep one timer task alive until it completes and compare the finished task by reference; or split into a timer loop and a channel loop that both call a `SemaphoreSlim(1,1)`-guarded scan method. Wrap the loop body in a catch-all that logs and continues.

#### C3. Stored XSS: AI-written HTML served and emailed unsanitized

- `HomeSeeker/Evaluation/AgentListingEvaluator.cs:189-195` accepts whatever the model returns as long as it contains `<html`.
- The report prompt is built from scraped listing titles/locations, the user's `ExtraCriteria`, and earlier AI output (`:208-263`), all of which an attacker can influence through a listing (prompt injection).
- `ReceptyOks.Api/Endpoints/HomeSeekerEndpoints.cs:168` serves it with `Results.Content(scanRun.ReportHtml, "text/html")` from the API origin, with no `Content-Security-Policy`, no `X-Content-Type-Options`, no sandbox. A `<script>` in the report runs with the API's origin.
- The same HTML goes out by email (phishing links, tracking pixels).
- Even the code fallback template does not encode `profile.City` (`:284`) or the `href` attribute (`:333`).

**Fix (pick one):** sanitize with an allow-list sanitizer (e.g. `HtmlSanitizer`) before storing — allow basic tags, inline `style`, `https:` links on known hosts only; **or** drop the AI-written report entirely and always use the code template (also removes one LLM call per scan and most of the injection surface). In both cases serve the endpoint with `Content-Security-Policy: default-src 'none'; style-src 'unsafe-inline'; img-src https:; sandbox` and `X-Content-Type-Options: nosniff`, and encode every interpolated value in the fallback template.

#### C4. SSRF and prompt injection through the web-browsing tool

- `ReceptyOks.Api/Services/AnthropicAiAgentFactory.cs:49-54` gives every evaluation agent a `WebBrowsingTool`, which registers both `fetch_web_page` and `search_web`.
- `ReceptyOks.Shared/AI/WebBrowsingTool.cs:70-124` fetches any `http(s)` URL: localhost, RFC1918 ranges, `169.254.169.254`, internal Azure endpoints. Redirects are followed. There is no host allow-list.
- The user prompt (`AgentListingEvaluator.cs:119-126`) interpolates scraped `Title` and `Location` verbatim, and the tool returns the full listing page. Nothing marks this as untrusted data. Instructions hidden in a listing can direct the agent to fetch internal URLs and leak the response through `search_web` query strings or the summary that ends up in the email.
- Evaluation does not need `search_web` at all.

**Fix:** for HomeSeeker, restrict `fetch_web_page` to an allow-list (`otodom.pl`, `olx.pl` and subdomains), resolve DNS and reject private/link-local addresses, disable auto-redirect (or re-validate each hop), do not register `search_web`, and wrap untrusted text in the prompt with explicit delimiters plus a "treat as data, not instructions" rule.

#### C5. Tool-round cap is a no-op (unbounded cost per evaluation)

- `ReceptyOks.Shared/AI/AiAgent.cs:118-154` accepts `maxToolRounds` in every `ChatAsync` overload but never applies it; the XML doc at `:118` even says so.
- `AgentListingEvaluator.cs:82` (`maxToolRounds: 4`) and `:186` (`maxToolRounds: 1`) therefore provide no cost bound. Combined with 50 000-character page fetches (`AnthropicAiAgentFactory.cs:52`), a single evaluation can loop far longer than intended.

**Fix:** wire the parameter to the function-invocation client's maximum iterations per request (or enforce it in the agent loop) and add a per-scan token/cost budget log line.

### High

#### H1. ScanRun stays `Running` forever on cancel or shutdown

- `HomeSeeker/Services/MarketScanService.cs:185-189` catches the cancellation, then `:199-201` calls `CompleteScanRunAsync(..., cancellationToken)` with the **same cancelled token**. `ListingRepository.CompleteScanRunAsync` (`ReceptyOks.Api/Repositories/ListingRepository.cs:92-104`) passes it to `ExecuteUpdateAsync`, which throws immediately. The row never leaves `Running`, and the exception escapes `RunScanAsync`.
- The scrapers swallow `OperationCanceledException` and return partial results (`OlxScraper.cs:87-90`, `OtodomScraper.cs:79-82`), so phases 2–4 continue after a cancel. The per-item catches at `MarketScanService.cs:81`, `:106`, `:144` then log one warning per listing.
- Nothing cleans up runs left in `Running` after a crash.

**Fix:** perform the final status write with `CancellationToken.None` (optionally a short timeout) inside its own try/catch; add `catch (OperationCanceledException) { throw; }` before the generic catches; on startup, mark stale `Running` runs older than N hours as `Failed`.

#### H2. On-demand scans are silently skipped

- `ListingRepository.TryMarkProfileScannedAsync` (`:46-63`) only succeeds when `LastScannedAt` is older than `ScanInterval`. It is a "due" check, not a lock.
- `HomeSeekerScanService.RunSingleScanAsync` (`:136-143`) calls it for on-demand scans too, so any `POST /profiles/{id}/scan` within 24 h of the last scan logs at Debug and does nothing — after the API already returned `202 Accepted` (`HomeSeekerEndpoints.cs:96`).
- Because the timestamp is set at the *start* of a scan, a failed scan blocks retries for a full interval.
- An inactive profile is accepted with 202 and then skipped in `MarketScanService.cs:47-51`.

**Fix:** separate a short lease (`ScanLeaseUntil`, e.g. 30 min) from `LastScannedAt`; on-demand scans acquire only the lease; set `LastScannedAt` on successful completion. Return 409 for inactive profiles and expose a scan-run id so the caller can poll.

#### H3. One failed insert poisons the rest of the scan

- `ListingRepository.UpsertListingAsync` (`:169-170`) adds the entity and saves. If `SaveChangesAsync` throws (e.g. unique-index race on `(SearchProfileId, Portal, ExternalId)`), the entity stays tracked as `Added` in the scoped `DbContext`.
- `MarketScanService.cs:106-110` catches and continues, but every later `SaveChangesAsync` in the same scope retries the same failed insert and fails too, so the remaining listings of that scan are lost quietly.

**Fix:** detach the entity (or `ChangeTracker.Clear()`) on failure, or better, load all existing listings for the profile in one query, compute changes in memory, and save once (also fixes M8).

#### H4. Unstable dedup key in the OLX HTML fallback

`HomeSeeker/Scrapers/OlxScraper.cs:337` uses `href.GetHashCode().ToString()` as `ExternalId` when no ID is found. `string.GetHashCode()` is randomized per process in .NET, so after every restart these listings look new, get re-inserted and re-evaluated (paid). **Fix:** derive a stable key (e.g. SHA-256 of the normalized path without query string) or skip cards without an ID.

#### H5. OLX JSON path is fragile and can abort the whole portal

- `OlxScraper.cs:151` runs `Regex.Unescape` on a JavaScript string literal. It is not a JS/JSON unescaper and throws `ArgumentException` on sequences it does not know. Only `JsonException` is caught (`:182`), so the exception reaches the outer catch at `:91` and ends OLX scraping **without** trying the HTML fallback.
- The lazy regex at `:412` (`"(.+?)"(?:;|<)`) stops at the first `\";` or `\"<` inside the escaped payload (common with HTML in descriptions), producing truncated JSON and silently zero results.
- `:224` `GetDecimal()` throws on null/string prices; the per-item catch drops the listing without a log.

**Fix:** decode with `JsonSerializer.Deserialize<string>("\"" + raw + "\"")`, locate the script with HtmlAgilityPack instead of a regex, and log dropped items at Debug with the reason.

#### H6. Explicit `null` pros/cons from the model loses the evaluation

`HomeSeeker/Models/ListingEvaluation.cs:21,26` default `Pros`/`Cons` to `[]`, but System.Text.Json overwrites them with `null` when the model returns `"pros": null`. `ListingRepository.SaveEvaluationAsync:185` then does `evaluation.Pros.Count` → `NullReferenceException`, caught at `MarketScanService.cs:144`, and the paid evaluation is discarded. **Fix:** normalize in the evaluator (`Pros = response.Pros ?? []`) or make the properties non-nullable with a `JsonIgnoreCondition`/converter.

#### H7. Decimal ordering and division in queries (*verify in Development*)

- `ListingRepository.cs:217` `ThenBy(l => l.Price / l.AreaSqm)` and `:245` `OrderBy(l => l.Price)`: EF Core's SQLite provider cannot translate decimal arithmetic or decimal `ORDER BY`. In Development (SQLite) `GetTopListingsAsync` is expected to throw, which fails the report phase of every scan. *Verify by running one scan locally.*
- On SQL Server the same expression raises divide-by-zero if any `AreaSqm` is `0`; `MarketScanService.cs:117` divides in memory with the same risk once a scraper stops filtering `area <= 0`.

**Fix:** store a computed `PricePerSqm` column at upsert time (guarding zero), order by it, and sort in memory where SQLite is used.

#### H8. No configuration exists anywhere

- Neither `appsettings.json` nor `appsettings.Development.json` has a `HomeSeeker` section; every value is a code default.
- `ReceptyOks.Api/.azure/main.bicep` defines no `HomeSeeker__*` environment variables and no SMTP secret, so in Azure scanning stays disabled and email is skipped ("SMTP not configured").
- The Anthropic `Token` secret is optional in Bicep (`main.bicep:200`); without it every `CreateAgent` throws and every scan ends `Failed`.
- `HomeSeekerOptions.Model` defaults to `"claude-opus-5-5"` (`HomeSeeker/Configuration/HomeSeekerOptions.cs:51`) while the comment and plan call for a cheaper model.
- SMTP settings live under `HomeSeeker:Smtp` (`:66`), but `IMPLEMENTATION_PLAN.md` phase 6 documents a top-level `Smtp` section and a `Smtp--Password` Key Vault secret. Following the plan would leave `Host` empty and silently disable email.

**Fix:** add the `HomeSeeker` section to both appsettings files, Bicep env vars (`HomeSeeker__Enabled`, `HomeSeeker__ScanInterval`, `HomeSeeker__Model`, `HomeSeeker__Smtp__*`) and a Key Vault secret `HomeSeeker--Smtp--Password`; default `Model` to a cheaper tier (Haiku 4.5 or Sonnet 5.5); update the plan to match the real section layout.

#### H9. Options are never validated

`ReceptyOks.Api/Extensions/HomeSeekerExtensions.cs:125` uses plain `Configure<HomeSeekerOptions>`. Consequences: `ScanInterval <= 0` makes `new PeriodicTimer` throw and stops the host (C2); negative `RequestDelay` makes `Task.Delay` throw inside the scrapers; `MaxSearchPagesPerPortal <= 0` silently scrapes nothing; no upper bounds on `MaxCandidatesPerScan`/`TopListingsInReport`; SMTP fields unchecked when enabled. **Fix:** DataAnnotations (`[Range]`, `[Required]`) plus `.ValidateDataAnnotations().ValidateOnStart()`, with a custom rule that SMTP is complete when `Enabled` is true.

#### H10. Zero tests, and integration tests never run in CI

- No file in `ReceptyOks_UnitTests` or `ReceptyOks.Api.IntegrationTests` references any HomeSeeker type.
- `.github/workflows/dotnet.yml:49-54` restores, builds and tests only `ReceptyOks_UnitTests.csproj`; the integration test project is never executed.
- `ReceptyOks.Api.IntegrationTests/CustomWebApplicationFactory.cs:49-73` swaps only `RecipeDbContext`. Under `Development`, `HomeSeekerDbContext` therefore points at the real SQLite file in `Data/`, `EnsureHomeSeekerTablesCreated` runs against it, and `HomeSeekerScanService` starts during tests.
- The `internal static` parsers (`OlxScraper.ParsePrerenderedState`, `OtodomScraper.ParseNextData`) were written "for unit testing", but there is no `InternalsVisibleTo` and no fixtures.

See section 4 for the recommended test set and reusable patterns.

### Medium

#### M1. Default resilience handler and header pollution on the scraper client

`ReceptyOksSolution.ServiceDefaults/Extensions.cs:29-32` applies `AddStandardResilienceHandler()` to every `HttpClient`, including `"homeseeker-scraper"`: three retries on 429/5xx against Otodom/OLX (raises ban risk) and a 10 s per-attempt timeout underneath the client's 30 s timeout (`HomeSeekerExtensions.cs:135`). `WebBrowsingTool.cs:37-40` also appends its own `User-Agent`/`Accept` to the shared client's default headers, producing a mixed, bot-looking UA. **Fix:** give the scraper client its own policy (no retry on 403/429, honour `Retry-After`), and give the tool a separate client it may configure.

#### M2. Scrapers are singletons holding one `HttpClient` forever

`HomeSeekerExtensions.cs:139-140` registers the scrapers as singletons, and they call `CreateClient` once in their constructors (`OtodomScraper.cs:28`, `OlxScraper.cs:29`). This defeats `IHttpClientFactory` handler rotation (stale DNS, no handler recycling). **Fix:** register as typed clients, or inject the factory and create the client per `SearchAsync`.

#### M3. Timeouts and bot challenges are invisible

- Both scrapers catch `OperationCanceledException` without checking the token (`OlxScraper.cs:87`, `OtodomScraper.cs:79`), so an HTTP timeout is logged at Debug as "scraping cancelled".
- 429 falls into the generic handler; `Retry-After` is ignored; there is no backoff or jitter.
- A `200 OK` bot-challenge page parses to zero listings and is logged at Debug as "No more listings".

**Fix:** `catch (OperationCanceledException) when (!ct.IsCancellationRequested)` → Warning; warn when page 1 yields zero results; detect challenge markers; handle 429 explicitly.

#### M4. No in-memory dedup of scraped listings

`MarketScanService.cs:68-88` concatenates all pages from all portals. OLX repeats promoted ads on every page and may serve the last page again for out-of-range page numbers, so `ListingsFoundCount` is inflated and the same listing is upserted several times per scan. There is also no cross-portal dedup (same house on OLX and Otodom → two evaluations). **Fix:** `DistinctBy(x => (x.Portal, x.ExternalId))` before upsert; consider a fuzzy match on (area, price, location) across portals.

#### M5. Report content and delisted listings

- `MarketScanService.cs:151` builds the report from all-time top listings, so the same five houses repeat every scan.
- Listings that disappear from the portals are never flagged although `LastSeenAt` is tracked, so sold houses stay in the report and in `GET /listings`.
- The email is sent even when nothing is new or discounted (`:164`); send failure is only logged, never recorded on the `ScanRun`.

**Fix:** report "new since last scan" plus "top overall, still listed" (`LastSeenAt >= scan start`); add an `IsActive`/`DelistedAt` flag; skip or shorten the email when there is nothing new; store `EmailSent` on the run.

#### M6. Candidates beyond the cap are never evaluated later

Selection (`MarketScanService.cs:115-119`) takes only `IsNew || PriceDropped`. A listing cut by `MaxCandidatesPerScan` is no longer "new" next time, so it is never evaluated. **Fix:** include the backlog `AiScore == null` (ordered by price/m²) after new/dropped items.

#### M7. Price-change tracking is lossy

`ListingRepository.cs:126-131` sets `PreviousPrice` on any decrease, never clears it on an increase, and treats any lower value (including parse noise) as a drop; the "↓ Obniżka!" badge can therefore be stale. **Fix:** keep a small price history or at least clear `PreviousPrice` when the price rises; require a minimum relative drop.

#### M8. Repository performance and indexes

- Two round trips per listing and a growing change tracker (`ListingRepository.cs:115-170`); hundreds of round trips per scan at five pages × two portals.
- Indexes do not match the queries (`HomeSeekerDbContext.cs:35-38`, `:66-69`, `:96-97`): single-column indexes on low-selectivity booleans, `UpdatedAt` indexed but never queried, while the hot queries need `(SearchProfileId, IsDeleted, AiScore)` and `(SearchProfileId, StartedAt)`.
- Paging sorts have no tiebreaker (`:244-247`), so rows can repeat or vanish between pages.

**Fix:** batch upsert (one `WHERE ExternalId IN (...)` query, one save), composite indexes, `ThenBy(l => l.Id)`.

#### M9. Column length overflows from unbounded input

`ScanRun.Error` is `HasMaxLength(2000)` but `MarketScanService.cs:193` stores `ex.Message` untruncated; `AiSummary` (2000) and `AiPriceAssessment` (100) come straight from the model. On SQL Server the `ExecuteUpdate` fails with a truncation error and the evaluation or the final status write is lost. **Fix:** truncate before writing; validate `PriceAssessment` against the allowed values.

#### M10. Table-creation probe is fragile; no migrations

`HomeSeekerExtensions.EnsureHomeSeekerTablesCreated` (`:95-106`) treats *any* exception from `SearchProfiles.Any()` as "tables missing", runs `CreateTables()`, which fails with "already exists" on a transient error, and the rethrow at `:111` crashes startup. The `!CanConnect → EnsureCreated` branch (`:86-92`) never runs because `EnsureDatabaseCreated()` for recipes executes first (`Program.cs:131`). Column changes need hand-written SQL (acknowledged at `:74`). **Fix:** probe `INFORMATION_SCHEMA.TABLES` / `sqlite_master`; consider adopting EF migrations for this context only.

#### M11. `ScanTriggerQueue` semantics

`ReceptyOks.Api/Services/ScanTriggerQueue.cs:19` uses `BoundedChannelFullMode.DropOldest`, so `TryWrite` never returns false, the 503 branch in `HomeSeekerEndpoints.cs:87-94` is dead, and the oldest requests are dropped silently. The same profile can be queued 100 times; `DequeueAsync` is unused. **Fix:** `FullMode = Wait` with `TryWrite`, de-duplicate pending IDs with a `ConcurrentDictionary`, delete the unused method.

#### M12. Endpoint hygiene

- Entities are returned directly (`HomeSeekerEndpoints.cs:29,37,45,63,129`), exposing `IsDeleted`, `LastScannedAt`, navigation collections and raw `AiProsJson`/`AiConsJson` strings. Define response DTOs.
- `GET /profiles/{id}` returns soft-deleted profiles because `GetProfileByIdAsync` (`ListingRepository.cs:40`) does not filter; PUT/DELETE return 404 for the same id. Add a global query filter or filter consistently.
- `GET /scans/{id}/report` does not check the owning profile's deletion state.
- Inconsistent 404 bodies (`:165` vs the rest); no `AddProblemDetails()` in `Program.cs`.
- No `.Produces<>()`/`.ProducesProblem()` metadata (compare `ShoppingListEndpoints.cs`); anonymous response objects give an empty OpenAPI schema.
- Handlers take no `CancellationToken`; `pageNumber` has no upper bound (`(pageNumber-1)*pageSize` can overflow); `sort` and `minScore` are not validated.

#### M13. Validator gaps

`ReceptyOks.Api/Validators/SearchProfileRequestValidator.cs:23-35`: the non-negative checks on `MinPrice`/`MinAreaSqm` run only when the matching max is also set, so a negative minimum alone passes. `EmailAddress()` in FluentValidation 11 only checks for an `@`; since the app sends mail to this address, use a stricter check (e.g. `MailAddress.TryCreate`).

#### M14. Secrets and PII in logs

`SmtpOptions` is a `record` (`HomeSeekerOptions.cs:72`), so its generated `ToString()` prints `Password`; `HomeSeekerOptions` is a record that contains it. Recipient addresses are logged at Information (`MarketScanService.cs:173,177`, `EmailScanReportSender.cs:76`). **Fix:** make `SmtpOptions` a class or override `PrintMembers` to mask the password; log a profile id instead of the email.

#### M15. Scheduling drift

`GetActiveProfilesAsync` and `TryMarkProfileScannedAsync` both use `cutoff = now - ScanInterval` (`ListingRepository.cs:27,50`) while the timer ticks at exactly `ScanInterval`. A profile scanned a few minutes after a tick is not yet due at the next tick and is skipped until the one after. **Fix:** tick more often (e.g. hourly) and rely on the cutoff, or subtract a tolerance.

#### M16. Prompt and report quality

- The system prompt says "ogłoszenia sprzedaży **mieszkań**" (apartments) (`AgentListingEvaluator.cs:21`) while the scanner searches for houses (`domy`).
- `PriceAssessment` values are defined only in prompt text and never validated.
- `MaxTokens = 4096` (`AnthropicAiAgentFactory.cs:40`) can truncate a full HTML report; the `Contains("<html")` check (`:189`) still passes truncated output; code fences are not stripped.
- `_options` is injected into the evaluator but unused (`:17,65`); `catch { }` at `:246,257` hides corrupted JSON.

#### M17. Dead and unused code

`EnableWebDiscovery` and `WebDiscoveryMaxResults` (`HomeSeekerOptions.cs:56,61`) are never read; plan phase 3.5 (`AgentWebDiscoveryScraper`) was never implemented. `ScanTriggerQueue.DequeueAsync` is unused. Either implement phase 3.5 or remove the options.

#### M18. Operability

- No health check for `HomeSeekerDbContext` and no liveness signal for the scan service (`Program.cs:71-72` checks only `RecipeDbContext`).
- No `BeginScope` with `ScanRunId`/`ProfileId`, so scraper, evaluator and sender logs cannot be correlated; no metrics for scrape counts, evaluations, tokens or cost per scan.
- `ScanRun.ReportHtml` is `NVARCHAR(MAX)` and retained forever on a 2 GB Basic SQL tier (`main.bicep:89-96`); nothing prunes old runs the way `ShoppingListCleaner` prunes shopping items.

#### M19. Agent factory lifecycle and secret reuse

`AnthropicAiAgentFactory.cs:44-45` creates a new `AnthropicAgent` and client per call and never disposes them. It reads the same `Token` secret that `TokenProviderEndpoints` hands out to clients, so a leaked client token is also the server's evaluation budget. **Fix:** cache the client, dispose agents, and use a dedicated Key Vault secret for server-side AI calls.

### Low

#### L1. Duplication and magic strings in scrapers
About 90 lines are duplicated between `OlxScraper` and `OtodomScraper` (the `SearchAsync` loop, `NormalizeCity`, error handling). Extract a `PagedPortalScraperBase` with abstract `BuildSearchUrl`/`Parse`. The client name `"homeseeker-scraper"` appears in three places and the portal names are literals; introduce constants.

#### L2. Interface granularity
`IListingRepository` has 14 methods mixing profile CRUD, scan runs, listings and locking; `IListingEvaluator` also writes reports. Split into `IProfileRepository`/`IListingStore`/`IScanRunStore` and `IScanReportWriter`.

#### L3. Culture and parsing edge cases
`decimal.TryParse` without `InvariantCulture` (`OlxScraper.cs:245,358`); `N0` formatting with the current culture in prompts; `AreaRegex` reads "1 200 m²" as 200; `ParsePriceString` fails on "1.234.567"; `Split('-')[0]` (`:369`) truncates "Bielsko-Biała".

#### L4. Otodom parsing robustness
`NextDataRegex` (`OtodomScraper.cs:242`) depends on attribute order and quoting; use HtmlAgilityPack's `//script[@id='__NEXT_DATA__']`. The slug and city are not URL-encoded; `District` is never used in either portal's search URL although the user can set it.

#### L5. SMTP implementation
`System.Net.Mail.SmtpClient` is marked obsolete by Microsoft (MailKit is recommended); `EnableSsl = true` is hard-coded (`EmailScanReportSender.cs:52`), so implicit-TLS port 465 cannot work. The class intentionally does not wrap `ReceptyOks.Shared` `MailSender` (the three bugs the plan lists there are already fixed), which is fine but should be stated in the plan.

#### L6. Small model/code issues
`ScanStatus` has no `Cancelled` value (cancellation is recorded as `Failed`); `DateTime.UtcNow` is used everywhere instead of `TimeProvider` (hurts testability); `$"..."` without placeholders at `AgentListingEvaluator.cs:117`.

#### L7. Documentation drift
`README.md` and `.github/copilot-instructions.md` do not mention HomeSeeker. `IMPLEMENTATION_PLAN.md` §8 says phases 3–7 are "do zrobienia", but 3, 4 and 5 are implemented (see section 6).

#### L8. Shared `AiAgent` nits
`AiAgent.cs:363` allocates a new `JsonSerializerOptions` per parse; `ExtractJsonFromResponse` handles code fences but not prose around bare JSON ("Here is the result: {...}" → `null`).

#### L9. Tangential
`.github/workflows/docker-lint.yml:16` points hadolint at a root `Dockerfile` that does not exist (with `continue-on-error`), so the real `ReceptyOks.Api/Dockerfile` is never linted.

## 4. Test & CI gaps

Nothing in HomeSeeker is covered. Recommended minimum set, reusing existing patterns:

| Area | Tests | Reuse |
|---|---|---|
| Scrapers | Embedded HTML fixtures for Otodom `__NEXT_DATA__` and OLX `__PRERENDERED_STATE__` + HTML fallback; malformed/empty HTML returns an empty list without throwing; one `SearchAsync` test with a stubbed handler (403, 429, timeout) | `ReceptyOks_UnitTests/WebBrowsingToolTests.cs` `CreateMockHandler` (~line 765); add `[InternalsVisibleTo("ReceptyOks_UnitTests")]` to `HomeSeeker.csproj` |
| `MarketScanService` | Candidate selection and cap; per-portal and per-listing fault isolation; cancellation leaves the run `Failed`/`Cancelled`, not `Running`; email sent / not sent; dedup of repeated scraped items | Moq for `IListingScraper`, `IListingRepository`, `IListingEvaluator`, `IScanReportSender` |
| `AgentListingEvaluator` | Valid JSON → evaluation; `null` pros/cons normalized; invalid JSON → `null`; report fallback on exception / truncated HTML | `ReceptyOks_UnitTests/AiAgentTests.cs` `Mock<IChatClient>` through a fake `IAiAgentFactory` |
| `ListingRepository` | Upsert new / existing / price drop; failed insert does not poison later saves; `TryMarkProfileScannedAsync` semantics; top-listings query works on SQLite (H7) | EF Core SQLite in-memory (`DataSource=:memory:`) |
| `HomeSeekerScanService` | On-demand wake does not throw; timer tick runs periodic scan once; loop survives a failing profile | Fake `IServiceScopeFactory`, short `ScanInterval` |
| Endpoints | CRUD, 400 from validation, 404 for deleted profiles, 202 scan, paging bounds, report content type and CSP header | `ReceptyOks.Api.IntegrationTests/CustomWebApplicationFactory.cs` — also swap `HomeSeekerDbContext` to in-memory and set `HomeSeeker:Enabled=false`, `HomeSeeker:StartupDelay=00:00:00` |

CI: add `ReceptyOks.Api.IntegrationTests` to `.github/workflows/dotnet.yml`; add `HomeSeeker/**` to the `docker-build.yml` path filters (C1).

## 5. Configuration & deployment gaps

| Item | Current state | Needed |
|---|---|---|
| `Dockerfile` | HomeSeeker not copied → build fails | Two `COPY` lines (C1) |
| `docker-build.yml` paths | No `HomeSeeker/**` | Add to push and PR filters |
| `appsettings.json` / `.Development.json` | No `HomeSeeker` section | `Enabled`, `ScanInterval`, `StartupDelay`, `MaxSearchPagesPerPortal`, `RequestDelay`, `MaxCandidatesPerScan`, `TopListingsInReport`, `Model`, `Smtp:{Host,Port,Login,FromAddress}` |
| User secrets (dev) | — | `HomeSeeker:Smtp:Password`, `Token` |
| `main.bicep` env | No `HomeSeeker__*` | `HomeSeeker__Enabled`, `HomeSeeker__ScanInterval`, `HomeSeeker__Model`, `HomeSeeker__Smtp__Host/Port/Login/FromAddress`, secret ref for password |
| Key Vault | No SMTP secret; `Token` optional | `HomeSeeker--Smtp--Password`; make `Token` required when `HomeSeeker__Enabled=true`; consider a dedicated server-side AI token (M19) |
| Options validation | None | `ValidateDataAnnotations().ValidateOnStart()` (H9) |
| Health checks | Recipes DB only | `AddDbContextCheck<HomeSeekerDbContext>("homeseeker-db")` |
| Replicas | `maxReplicas: 1` (`main.bicep:346-347`) | Keep at 1 until the lease logic in H2 exists |
| Data retention | None | Scheduled cleanup of old `ScanRun.ReportHtml` and delisted listings |

## 6. Plan vs. code status (corrects `IMPLEMENTATION_PLAN.md` §8)

| Phase | Plan table says | Actual |
|---|---|---|
| 1. Library (models, options, scrapers) | done | done |
| 2. Persistence | partial | done — `HomeSeekerExtensions` and `ListingRepository` exist |
| 3. AI evaluation | todo | done, with C3, C4, C5, H6 issues |
| 3.5. Web discovery (optional) | todo | not started; options exist but are unused |
| 4. Orchestration + email + scheduler | todo | done, with C2, H1, H2 issues; the three `MailSender` fixes are done; `EmailScanReportSender` is standalone (does not wrap `MailSender`) |
| 5. API | todo | done, with H11, M12 issues |
| 6. Config + Bicep | todo | **not started** |
| 7. Tests | todo | **not started** |

## 7. Suggested fix order

1. **Make it deployable** — C1 (Dockerfile + workflow paths), H8 (config, Bicep, secrets, cheaper default model), H9 (options validation).
2. **Make it stable and safe** — C2 (scan loop), H1 (stuck runs), H2 (on-demand lease), H3 (poisoned context), C3 (sanitize or drop AI HTML + CSP), C4 (tool allow-list, no `search_web`, prompt delimiters), C5 (tool-round cap), M14 (password in `ToString`).
3. **Make results correct** — H4, H5, H6, H7, M3, M4, M5, M6, M7, M9, M15, M16.
4. **Raise quality** — H10 (tests + CI), H11 (ownership / per-client limits), M1, M2, M8, M10, M11, M12, M13, M17, M18, M19, and the Low items as time allows.

Items 1–2 are small, mostly local changes; item 3 touches scrapers and repository logic; item 4 is ongoing hardening.
