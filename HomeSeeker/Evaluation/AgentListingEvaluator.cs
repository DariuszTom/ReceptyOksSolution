using System.Text;
using System.Text.Json;
using HomeSeeker.Abstractions;
using HomeSeeker.Configuration;
using HomeSeeker.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HomeSeeker.Evaluation;

/// <summary>
/// AI-based listing evaluator using AiAgent with WebBrowsingTool.
/// </summary>
public sealed class AgentListingEvaluator : IListingEvaluator
{
    private readonly IAiAgentFactory _agentFactory;
    private readonly HomeSeekerOptions _options;
    private readonly ILogger<AgentListingEvaluator> _logger;

    private const string EvaluationSystemPrompt = """
        Jesteś ekspertem od nieruchomości w Polsce. Analizujesz ogłoszenia sprzedaży mieszkań.

        ZAWSZE odpowiadaj TYLKO poprawnym JSON w formacie:
        {
            "score": <liczba 0-100>,
            "summary": "<krótkie podsumowanie 2-3 zdania>",
            "pros": ["<zaleta 1>", "<zaleta 2>", ...],
            "cons": ["<wada 1>", "<wada 2>", ...],
            "priceAssessment": "<ocena ceny: 'okazja'|'uczciwa cena'|'lekko zawyżona'|'zawyżona'|'mocno zawyżona'>"
        }

        Oceniaj pod kątem:
        - Lokalizacja i okolica
        - Stan techniczny i wiek budynku
        - Układ pomieszczeń
        - Dodatkowe udogodnienia (garaż, ogród, piwnica)
        - Cena za metr kwadratowy w porównaniu do rynku

        NIE dodawaj żadnego tekstu przed ani po JSON. TYLKO JSON.
        """;

    private const string ReportSystemPrompt = """
        Jesteś ekspertem od nieruchomości. Tworzysz profesjonalne raporty HTML.

        ZAWSZE odpowiadaj TYLKO poprawnym kodem HTML. Bez markdown, bez ```html.

        Styl:
        - Inline CSS (email-safe)
        - Czytelna tabela z rankingiem
        - Polskie nazwy i formatowanie cen (np. "750 000 zł")
        - Linki do ogłoszeń jako przyciski
        - Zwięzłe opisy i zalety/wady jako listy
        - Responsywny layout

        NIE dodawaj żadnego tekstu przed ani po HTML. TYLKO HTML.
        """;

    public AgentListingEvaluator(
        IAiAgentFactory agentFactory,
        IOptions<HomeSeekerOptions> options,
        ILogger<AgentListingEvaluator> logger)
    {
        ArgumentNullException.ThrowIfNull(agentFactory);
        _agentFactory = agentFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ListingEvaluation?> EvaluateAsync(
        SearchProfile profile,
        HouseListing listing,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var agent = _agentFactory.CreateAgent(EvaluationSystemPrompt, withWebBrowsing: true);

            var prompt = BuildEvaluationPrompt(profile, listing);

            _logger.LogDebug("Evaluating listing {ListingId} from {Portal}", listing.Id, listing.Portal);

            var response = await agent.ChatAsync<ListingEvaluation>(prompt, maxToolRounds: 4, cancellationToken)
                .ConfigureAwait(false);

            if (response is null)
            {
                _logger.LogWarning("AI evaluation returned null for listing {ListingId}", listing.Id);
                return null;
            }

            // Validate score range
            if (response.Score < 0 || response.Score > 100)
            {
                _logger.LogWarning("AI evaluation returned invalid score {Score} for listing {ListingId}",
                    response.Score, listing.Id);
                return response with { Score = Math.Clamp(response.Score, 0, 100) };
            }

            return response;
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Evaluation cancelled for listing {ListingId}", listing.Id);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI evaluation failed for listing {ListingId}", listing.Id);
            return null;
        }
    }

    private static string BuildEvaluationPrompt(SearchProfile profile, HouseListing listing)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"Oceń ogłoszenie domu na sprzedaż.");
        sb.AppendLine();
        sb.AppendLine($"URL ogłoszenia: {listing.Url}");
        sb.AppendLine($"Tytuł: {listing.Title}");
        sb.AppendLine($"Cena: {listing.Price:N0} zł");
        sb.AppendLine($"Powierzchnia: {listing.AreaSqm:N0} m²");
        sb.AppendLine($"Cena za m²: {listing.Price / listing.AreaSqm:N0} zł/m²");

        if (!string.IsNullOrWhiteSpace(listing.Location))
            sb.AppendLine($"Lokalizacja: {listing.Location}");

        sb.AppendLine();
        sb.AppendLine("Kryteria kupującego:");
        sb.AppendLine($"- Miasto: {profile.City}");

        if (!string.IsNullOrWhiteSpace(profile.District))
            sb.AppendLine($"- Dzielnica: {profile.District}");

        if (profile.MinPrice.HasValue || profile.MaxPrice.HasValue)
        {
            var priceRange = profile.MinPrice.HasValue && profile.MaxPrice.HasValue
                ? $"{profile.MinPrice:N0} - {profile.MaxPrice:N0} zł"
                : profile.MaxPrice.HasValue
                    ? $"do {profile.MaxPrice:N0} zł"
                    : $"od {profile.MinPrice:N0} zł";
            sb.AppendLine($"- Budżet: {priceRange}");
        }

        if (profile.MinAreaSqm.HasValue || profile.MaxAreaSqm.HasValue)
        {
            var areaRange = profile.MinAreaSqm.HasValue && profile.MaxAreaSqm.HasValue
                ? $"{profile.MinAreaSqm:N0} - {profile.MaxAreaSqm:N0} m²"
                : profile.MaxAreaSqm.HasValue
                    ? $"do {profile.MaxAreaSqm:N0} m²"
                    : $"od {profile.MinAreaSqm:N0} m²";
            sb.AppendLine($"- Powierzchnia: {areaRange}");
        }

        if (!string.IsNullOrWhiteSpace(profile.ExtraCriteria))
        {
            sb.AppendLine();
            sb.AppendLine($"Dodatkowe wymagania: {profile.ExtraCriteria}");
        }

        sb.AppendLine();
        sb.AppendLine("Użyj narzędzia fetch_web_page aby pobrać szczegóły ogłoszenia, a następnie oceń je.");

        return sb.ToString();
    }

    public async Task<string> WriteReportHtmlAsync(
        SearchProfile profile,
        IReadOnlyList<HouseListing> listings,
        ScanRun scanRun,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (listings.Count == 0)
            {
                return GenerateFallbackReport(profile, listings, scanRun);
            }

            var agent = _agentFactory.CreateAgent(ReportSystemPrompt, withWebBrowsing: false);

            var prompt = BuildReportPrompt(profile, listings, scanRun);

            _logger.LogDebug("Generating HTML report for profile {ProfileId}", profile.Id);

            var response = await agent.ChatAsync(prompt, maxToolRounds: 1, cancellationToken)
                .ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(response) || !response.Contains("<html", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("AI report generation returned invalid HTML, using fallback");
                return GenerateFallbackReport(profile, listings, scanRun);
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI report generation failed, using fallback");
            return GenerateFallbackReport(profile, listings, scanRun);
        }
    }

    private static string BuildReportPrompt(SearchProfile profile, IReadOnlyList<HouseListing> listings, ScanRun scanRun)
    {
        var sb = new StringBuilder();

        sb.AppendLine("Stwórz HTML raport z wynikami skanowania rynku nieruchomości.");
        sb.AppendLine();
        sb.AppendLine($"Profil wyszukiwania:");
        sb.AppendLine($"- Miasto: {profile.City}");
        if (!string.IsNullOrWhiteSpace(profile.District))
            sb.AppendLine($"- Dzielnica: {profile.District}");
        sb.AppendLine();
        sb.AppendLine($"Statystyki skanu:");
        sb.AppendLine($"- Data: {scanRun.StartedAt:yyyy-MM-dd HH:mm}");
        sb.AppendLine($"- Znaleziono ogłoszeń: {scanRun.ListingsFoundCount}");
        sb.AppendLine($"- Nowych: {scanRun.NewListingsCount}");
        sb.AppendLine($"- Z obniżką ceny: {scanRun.PriceDropsCount}");
        sb.AppendLine($"- Ocenionych przez AI: {scanRun.EvaluatedCount}");
        sb.AppendLine();
        sb.AppendLine("TOP ogłoszenia (posortowane wg oceny AI):");
        sb.AppendLine();

        foreach (var (listing, index) in listings.Select((l, i) => (l, i + 1)))
        {
            sb.AppendLine($"#{index}. {listing.Title}");
            sb.AppendLine($"   URL: {listing.Url}");
            sb.AppendLine($"   Cena: {listing.Price:N0} zł");
            sb.AppendLine($"   Powierzchnia: {listing.AreaSqm:N0} m²");
            sb.AppendLine($"   Cena/m²: {listing.Price / listing.AreaSqm:N0} zł/m²");
            sb.AppendLine($"   Ocena AI: {listing.AiScore}/100");
            sb.AppendLine($"   Ocena ceny: {listing.AiPriceAssessment ?? "brak"}");

            if (!string.IsNullOrWhiteSpace(listing.AiSummary))
                sb.AppendLine($"   Podsumowanie: {listing.AiSummary}");

            if (!string.IsNullOrWhiteSpace(listing.AiProsJson))
            {
                try
                {
                    var pros = JsonSerializer.Deserialize<List<string>>(listing.AiProsJson);
                    if (pros?.Count > 0)
                        sb.AppendLine($"   Zalety: {string.Join(", ", pros)}");
                }
                catch { }
            }

            if (!string.IsNullOrWhiteSpace(listing.AiConsJson))
            {
                try
                {
                    var cons = JsonSerializer.Deserialize<List<string>>(listing.AiConsJson);
                    if (cons?.Count > 0)
                        sb.AppendLine($"   Wady: {string.Join(", ", cons)}");
                }
                catch { }
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string GenerateFallbackReport(SearchProfile profile, IReadOnlyList<HouseListing> listings, ScanRun scanRun)
    {
        var sb = new StringBuilder();

        sb.AppendLine("""
            <!DOCTYPE html>
            <html lang="pl">
            <head>
                <meta charset="UTF-8">
                <meta name="viewport" content="width=device-width, initial-scale=1.0">
                <title>Raport HomeSeeker</title>
            </head>
            <body style="font-family: Arial, sans-serif; max-width: 800px; margin: 0 auto; padding: 20px; background-color: #f5f5f5;">
                <div style="background-color: white; padding: 30px; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1);">
            """);

        sb.AppendLine($"""
                    <h1 style="color: #2c3e50; border-bottom: 2px solid #3498db; padding-bottom: 10px;">
                        🏠 Raport HomeSeeker - {profile.City}
                    </h1>

                    <div style="background-color: #ecf0f1; padding: 15px; border-radius: 5px; margin-bottom: 20px;">
                        <p style="margin: 5px 0;"><strong>Data skanu:</strong> {scanRun.StartedAt:yyyy-MM-dd HH:mm}</p>
                        <p style="margin: 5px 0;"><strong>Znaleziono:</strong> {scanRun.ListingsFoundCount} ogłoszeń</p>
                        <p style="margin: 5px 0;"><strong>Nowych:</strong> {scanRun.NewListingsCount}</p>
                        <p style="margin: 5px 0;"><strong>Z obniżką:</strong> {scanRun.PriceDropsCount}</p>
                        <p style="margin: 5px 0;"><strong>Ocenionych:</strong> {scanRun.EvaluatedCount}</p>
                    </div>
            """);

        if (listings.Count == 0)
        {
            sb.AppendLine("""
                    <p style="color: #7f8c8d; font-style: italic;">Brak ogłoszeń do wyświetlenia.</p>
            """);
        }
        else
        {
            sb.AppendLine("""
                    <h2 style="color: #2c3e50;">TOP Ogłoszenia</h2>
                    <table style="width: 100%; border-collapse: collapse; margin-top: 15px;">
                        <thead>
                            <tr style="background-color: #3498db; color: white;">
                                <th style="padding: 12px; text-align: left;">#</th>
                                <th style="padding: 12px; text-align: left;">Tytuł</th>
                                <th style="padding: 12px; text-align: right;">Cena</th>
                                <th style="padding: 12px; text-align: right;">m²</th>
                                <th style="padding: 12px; text-align: center;">Ocena</th>
                            </tr>
                        </thead>
                        <tbody>
            """);

            foreach (var (listing, index) in listings.Select((l, i) => (l, i + 1)))
            {
                var bgColor = index % 2 == 0 ? "#f9f9f9" : "white";
                var scoreColor = listing.AiScore switch
                {
                    >= 80 => "#27ae60",
                    >= 60 => "#f39c12",
                    _ => "#e74c3c"
                };

                sb.AppendLine($"""
                            <tr style="background-color: {bgColor};">
                                <td style="padding: 12px;">{index}</td>
                                <td style="padding: 12px;">
                                    <a href="{listing.Url}" style="color: #3498db; text-decoration: none; font-weight: bold;">
                                        {System.Net.WebUtility.HtmlEncode(listing.Title)}
                                    </a>
                                    {(listing.PreviousPrice.HasValue && listing.PreviousPrice > listing.Price ? "<br><span style=\"color: #27ae60; font-size: 12px;\">↓ Obniżka!</span>" : "")}
                                </td>
                                <td style="padding: 12px; text-align: right; font-weight: bold;">{listing.Price:N0} zł</td>
                                <td style="padding: 12px; text-align: right;">{listing.AreaSqm:N0}</td>
                                <td style="padding: 12px; text-align: center;">
                                    <span style="background-color: {scoreColor}; color: white; padding: 4px 8px; border-radius: 4px; font-weight: bold;">
                                        {listing.AiScore ?? 0}
                                    </span>
                                </td>
                            </tr>
                """);

                if (!string.IsNullOrWhiteSpace(listing.AiSummary))
                {
                    sb.AppendLine($"""
                            <tr style="background-color: {bgColor};">
                                <td></td>
                                <td colspan="4" style="padding: 0 12px 12px 12px; color: #7f8c8d; font-size: 13px;">
                                    {System.Net.WebUtility.HtmlEncode(listing.AiSummary)}
                                </td>
                            </tr>
                    """);
                }
            }

            sb.AppendLine("""
                        </tbody>
                    </table>
            """);
        }

        sb.AppendLine("""
                    <p style="margin-top: 30px; color: #95a5a6; font-size: 12px; text-align: center;">
                        Wygenerowano automatycznie przez HomeSeeker
                    </p>
                </div>
            </body>
            </html>
        """);

        return sb.ToString();
    }
}
