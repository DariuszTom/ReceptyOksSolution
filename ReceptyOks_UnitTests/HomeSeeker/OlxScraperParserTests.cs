using HomeSeeker.Scrapers;

namespace ReceptyOks_UnitTests.HomeSeeker;

/// <summary>
/// Fixture tests for the OLX parsers so that portal markup changes cannot silently
/// turn every page into zero results.
/// </summary>
public sealed class OlxScraperParserTests
{
    private const string PrerenderedStateHtml = """
        <html><head>
        <script>
        window.__PRERENDERED_STATE__= "{\"listing\":{\"listing\":{\"ads\":[{\"id\":123456789,\"url\":\"https://www.olx.pl/d/oferta/mieszkanie-ID123456789.html\",\"title\":\"Mieszkanie 3 pokoje\",\"price\":{\"regularPrice\":{\"value\":650000}},\"params\":[{\"key\":\"m\",\"value\":{\"key\":\"62.5\"}}],\"location\":{\"city\":{\"name\":\"Warszawa\"},\"district\":{\"name\":\"Mokot\u00f3w\"}}},{\"id\":987654321,\"url\":\"/d/oferta/kawalerka-ID987654321.html\",\"title\":\"Kawalerka\",\"price\":{\"displayValue\":\"350 000 z\u0142\"},\"params\":[{\"key\":\"m\",\"value\":{\"key\":\"28\"}}],\"location\":{\"city\":{\"name\":\"Krak\u00f3w\"}}},{\"id\":111,\"url\":\"/d/oferta/brak-ceny-ID111.html\",\"title\":\"Brak ceny\",\"params\":[{\"key\":\"m\",\"value\":{\"key\":\"40\"}}]}]}}}";
        </script>
        </head><body></body></html>
        """;

    private const string HtmlFallbackPage = """
        <html><body>
        <div data-cy="l-card">
          <a href="/d/oferta/dom-wolnostojacy-ID555555.html">
            <h6>Dom wolnostoj&#261;cy</h6>
          </a>
          <p data-testid="ad-price">1 200 000 zł</p>
          <div class="params"><span>140 m²</span><span>5 pokoi</span></div>
          <p data-testid="location-date">Gdańsk, Oliwa - Odświeżono dzisiaj</p>
        </div>
        <div data-cy="l-card">
          <a href="https://www.olx.pl/d/oferta/mieszkanie-bez-id.html">
            <h6>Mieszkanie bez ID</h6>
          </a>
          <p data-testid="ad-price">420 000 zł</p>
          <div class="params"><span>55 m²</span></div>
        </div>
        <div data-cy="l-card">
          <a href="/d/oferta/brak-ceny-ID777.html"><h6>Brak ceny</h6></a>
          <div class="params"><span>70 m²</span></div>
        </div>
        </body></html>
        """;

    #region ParsePrerenderedState

    [Test]
    public void ParsePrerenderedState_ValidFixture_ReturnsListingsWithValidPriceAndArea()
    {
        var results = OlxScraper.ParsePrerenderedState(PrerenderedStateHtml);

        Assert.That(results, Has.Count.EqualTo(2), "listing without price must be skipped");

        var first = results[0];
        Assert.Multiple(() =>
        {
            Assert.That(first.Portal, Is.EqualTo("OLX"));
            Assert.That(first.ExternalId, Is.EqualTo("123456789"));
            Assert.That(first.Url, Is.EqualTo("https://www.olx.pl/d/oferta/mieszkanie-ID123456789.html"));
            Assert.That(first.Title, Is.EqualTo("Mieszkanie 3 pokoje"));
            Assert.That(first.Price, Is.EqualTo(650000m));
            Assert.That(first.AreaSqm, Is.EqualTo(62.5m));
            Assert.That(first.Location, Is.EqualTo("Warszawa, Mokotów"));
        });

        var second = results[1];
        Assert.Multiple(() =>
        {
            Assert.That(second.ExternalId, Is.EqualTo("987654321"));
            Assert.That(second.Url, Is.EqualTo("https://www.olx.pl/d/oferta/kawalerka-ID987654321.html"), "relative URL must be absolutised");
            Assert.That(second.Price, Is.EqualTo(350000m), "displayValue fallback must be parsed");
            Assert.That(second.AreaSqm, Is.EqualTo(28m));
            Assert.That(second.Location, Is.EqualTo("Kraków"));
        });
    }

    [Test]
    public void ParsePrerenderedState_NoPrerenderedState_ReturnsEmpty()
    {
        var results = OlxScraper.ParsePrerenderedState("<html><body><p>Nothing here</p></body></html>");

        Assert.That(results, Is.Empty);
    }

    [Test]
    public void ParsePrerenderedState_MalformedJson_ReturnsEmptyWithoutThrowing()
    {
        const string html = """
            <script>window.__PRERENDERED_STATE__ = "{\"listing\":{\"listing\":{\"ads\":[{\"id\":1,";</script>
            """;

        var results = OlxScraper.ParsePrerenderedState(html);

        Assert.That(results, Is.Empty);
    }

    [Test]
    public void ParsePrerenderedState_InvalidEscapeSequence_ReturnsEmptyWithoutThrowing()
    {
        const string html = """
            <script>window.__PRERENDERED_STATE__ = "{\"listing\":\z}";</script>
            """;

        var results = OlxScraper.ParsePrerenderedState(html);

        Assert.That(results, Is.Empty);
    }

    [Test]
    public void ParsePrerenderedState_UnknownStructure_ReturnsEmpty()
    {
        const string html = """
            <script>window.__PRERENDERED_STATE__ = "{\"somethingElse\":{\"items\":[]}}";</script>
            """;

        var results = OlxScraper.ParsePrerenderedState(html);

        Assert.That(results, Is.Empty);
    }

    #endregion

    #region ParseHtmlFallback

    [Test]
    public void ParseHtmlFallback_ValidFixture_ReturnsListings()
    {
        var results = OlxScraper.ParseHtmlFallback(HtmlFallbackPage);

        Assert.That(results, Has.Count.EqualTo(2), "card without price must be skipped");

        var first = results[0];
        Assert.Multiple(() =>
        {
            Assert.That(first.Portal, Is.EqualTo("OLX"));
            Assert.That(first.ExternalId, Is.EqualTo("555555"));
            Assert.That(first.Url, Is.EqualTo("https://www.olx.pl/d/oferta/dom-wolnostojacy-ID555555.html"));
            Assert.That(first.Title, Is.EqualTo("Dom wolnostojący"), "HTML entities must be decoded");
            Assert.That(first.Price, Is.EqualTo(1200000m));
            Assert.That(first.AreaSqm, Is.EqualTo(140m));
            Assert.That(first.Location, Is.EqualTo("Gdańsk, Oliwa"));
        });
    }

    [Test]
    public void ParseHtmlFallback_CardWithoutIdInUrl_UsesDeterministicFallbackId()
    {
        var expectedId = OlxScraper.ComputeFallbackId("https://www.olx.pl/d/oferta/mieszkanie-bez-id.html");

        var first = OlxScraper.ParseHtmlFallback(HtmlFallbackPage);
        var second = OlxScraper.ParseHtmlFallback(HtmlFallbackPage);

        Assert.Multiple(() =>
        {
            Assert.That(first[1].ExternalId, Is.EqualTo(expectedId));
            Assert.That(second[1].ExternalId, Is.EqualTo(expectedId), "fallback ID must be stable across parses");
        });
    }

    [Test]
    public void ParseHtmlFallback_NoCards_ReturnsEmpty()
    {
        var results = OlxScraper.ParseHtmlFallback("<html><body><div>no cards</div></body></html>");

        Assert.That(results, Is.Empty);
    }

    [Test]
    public void ParseHtmlFallback_EmptyHtml_ReturnsEmptyWithoutThrowing()
    {
        var results = OlxScraper.ParseHtmlFallback(string.Empty);

        Assert.That(results, Is.Empty);
    }

    #endregion

    #region ComputeFallbackId

    [Test]
    public void ComputeFallbackId_SameUrl_ReturnsSameId()
    {
        const string url = "https://www.olx.pl/d/oferta/test.html";

        Assert.That(OlxScraper.ComputeFallbackId(url), Is.EqualTo(OlxScraper.ComputeFallbackId(url)));
    }

    [Test]
    public void ComputeFallbackId_DifferentUrls_ReturnDifferentIds()
    {
        var a = OlxScraper.ComputeFallbackId("https://www.olx.pl/d/oferta/a.html");
        var b = OlxScraper.ComputeFallbackId("https://www.olx.pl/d/oferta/b.html");

        Assert.That(a, Is.Not.EqualTo(b));
    }

    [Test]
    public void ComputeFallbackId_IsPrefixedAndBounded()
    {
        var id = OlxScraper.ComputeFallbackId("https://www.olx.pl/d/oferta/x.html");

        Assert.Multiple(() =>
        {
            Assert.That(id, Does.StartWith("url-"));
            Assert.That(id, Has.Length.EqualTo(4 + 32));
        });
    }

    #endregion
}
