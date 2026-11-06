# Copilot Instructions

## Solution overview
- .NET 10 solution orchestrated with .NET Aspire (`ReceptyOksSolution.AppHost`, `ServiceDefaults`).
- `ReceptyOks` – .NET MAUI app (Android, Windows) with Blazor Hybrid pages.
- `ReceptyOks.Api` – ASP.NET Core API (EF Core, JWT auth, FluentValidation, Scalar/OpenAPI, YARP).
- `ReceptyOks.Shared` – shared models, enums, AI abstractions (Microsoft.Extensions.AI, Microsoft.Agents.AI).
- `ReceptyOks.BlazorComponents` – Razor components (MudBlazor, ApexCharts).
- `HomeSeeker` – property-scraping library.
- Tests: `ReceptyOks_UnitTests`, `ReceptyOks.Api.IntegrationTests` (NUnit + Moq).

## General guidelines
- Target .NET 10 / C# latest; use file-scoped namespaces, nullable reference types, primary constructors where it improves readability.
- Prefer NuGet packages over custom implementations.
- Use async/await end-to-end; don't use `.Result`/`.Wait()`. Use `SafeFireAndForget` (AsyncAwaitBestPractices) for fire-and-forget.
- Register services through DI extension methods (e.g., in `Configuration/`), not with `new` inside view models.
- Log through `ILogger<T>` (Serilog backend); never log secrets or tokens.
- Prefer `System.Text.Json` for new code; use Newtonsoft only where it's already used.
- User-facing text is in Polish; code identifiers are in English, except for existing domain names.

## .NET MAUI
- Use only .NET MAUI APIs and patterns, never Xamarin.Forms ones. Don't add new usages of `Microsoft.Maui.Controls.Compatibility`.
- MVVM with CommunityToolkit.Mvvm: `[ObservableProperty]`, `[RelayCommand]`, `partial` classes; no logic in code-behind.
- Use compiled bindings (`x:DataType`) in XAML.
- UI controls: UraniumUI (Material) and CommunityToolkit.Maui come first.
- Colors come from `ReceptyOks\Resources\Styles\Colors.xaml` and styles from `Styles.xaml`; never hardcode either in XAML or C#.
- Local storage uses sqlite-net-pcl via `Data\LocalDatabase.cs`.

## API
- Validate requests with FluentValidation; keep controllers/endpoints thin.
- Secrets come from Azure Key Vault or user-secrets, never from appsettings.

## AI
- Use the `Units` enum (`ReceptyOks.Shared`) to build unit lists in AI prompts programmatically (e.g., `EnumHelpers.ToList<Units>()`); don't hardcode unit names.
- Go through `IChatClient` (Microsoft.Extensions.AI) and `AiChatClientFactory` rather than calling provider SDKs directly.

## Testing
- NUnit with `Assert.That` constraint syntax; Moq for mocks.
- Name tests `Method_Scenario_ExpectedResult`.
- Add or update tests whenever behavior changes.