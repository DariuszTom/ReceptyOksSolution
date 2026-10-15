using HomeSeeker.Scrapers;

namespace ReceptyOks_UnitTests.HomeSeeker;

/// <summary>
/// Fixture tests for the Otodom __NEXT_DATA__ parser so that portal markup changes
/// cannot silently turn every page into zero results.
/// </summary>
public sealed class OtodomScraperParserTests
{
    private const string NextDataHtml = """
        <html><head>
        <script id="__NEXT_DATA__" type="application/json">
        {"props":{"pageProps":{"data":{"searchAds":{"items":[
          {"id":64512345,"slug":"dom-wolnostojacy-ID4abc","title":"Dom wolnostojący 150 m²","totalPrice":{"value":1250000,"currency":"PLN"},"areaInSquareMeters":150.5,"location":{"address":{"city":{"name":"Gdańsk"},"district":{"name":"Oliwa"}}}},
          {"id":64598765,"slug":"segment-ID4def","title":"Segment","totalPrice":{"value":890000},"areaInSquareMeters":120,"location":{"address":{"city":{"name":"Sopot"}}}},
          {"id":64500001,"slug":"brak-ceny-ID4ghi","title":"Zapytaj o cenę","totalPrice":null,"areaInSquareMeters":200}
        ]}}}}}
        </script>
        </head><body></body></html>
        """;

    [Test]
    public void ParseNextData_WithValidFixture_ReturnsListingsWithParsedFields()
    {
        var listings = OtodomScraper.ParseNextData(NextDataHtml);

        Assert.That(listings, Has.Count.EqualTo(2), "listing without a price must be skipped");

        var first = listings[0];
        Assert.Multiple(() =>
        {
            Assert.That(first.Portal, Is.EqualTo("Otodom"));
            Assert.That(first.ExternalId, Is.EqualTo("64512345"));
            Assert.That(first.Url, Is.EqualTo("https://www.otodom.pl/pl/oferta/dom-wolnostojacy-ID4abc"));
            Assert.That(first.Title, Is.EqualTo("Dom wolnostojący 150 m²"));
            Assert.That(first.Price, Is.EqualTo(1250000m));
            Assert.That(first.AreaSqm, Is.EqualTo(150.5m));
            Assert.That(first.Location, Is.EqualTo("Gdańsk, Oliwa"));
        });

        var second = listings[1];
        Assert.Multiple(() =>
        {
            Assert.That(second.ExternalId, Is.EqualTo("64598765"));
            Assert.That(second.Price, Is.EqualTo(890000m));
            Assert.That(second.AreaSqm, Is.EqualTo(120m));
            Assert.That(second.Location, Is.EqualTo("Sopot"), "district is optional");
        });
    }

    [Test]
    public void ParseNextData_CurrentFormat_ReadsLocationFromReverseGeocoding()
    {
        const string html = """
            <script id="__NEXT_DATA__" type="application/json">
            {"props":{"pageProps":{"data":{"searchAds":{"items":[
              {"id":68529519,"slug":"willa-ID4DxEP","title":"Willa","totalPrice":{"value":2512750},"areaInSquareMeters":218.5,
               "location":{"address":{"street":{"name":"Królowej Jadwigi"},"city":null,"province":null},
                 "reverseGeocoding":{"locations":[
                   {"locationLevel":"voivodeship","name":"małopolskie"},
                   {"locationLevel":"city_or_village","name":"Kraków"},
                   {"locationLevel":"district","name":"Zwierzyniec"},
                   {"locationLevel":"residential","name":"Zwierzyniec"}]}}}
            ]}}}}}
            </script>
            """;

        var listings = OtodomScraper.ParseNextData(html);

        Assert.That(listings, Has.Count.EqualTo(1));
        Assert.That(listings[0].Location, Is.EqualTo("Kraków, Zwierzyniec"));
    }

    [Test]
    public void ParseNextData_WithoutNextDataScript_ReturnsEmpty()
    {
        var listings = OtodomScraper.ParseNextData("<html><body><p>Nothing here</p></body></html>");

        Assert.That(listings, Is.Empty);
    }

    [Test]
    public void ParseNextData_WithMalformedJson_ReturnsEmptyWithoutThrowing()
    {
        const string html = """
            <script id="__NEXT_DATA__" type="application/json">{"props":{"pageProps":{</script>
            """;

        Assert.DoesNotThrow(() => OtodomScraper.ParseNextData(html));
        Assert.That(OtodomScraper.ParseNextData(html), Is.Empty);
    }

    [Test]
    public void ParseNextData_WithUnexpectedStructure_ReturnsEmpty()
    {
        const string html = """
            <script id="__NEXT_DATA__" type="application/json">{"props":{"pageProps":{"somethingElse":[]}}}</script>
            """;

        Assert.That(OtodomScraper.ParseNextData(html), Is.Empty);
    }

    [Test]
    public void ParseNextData_SkipsItemsMissingRequiredFields()
    {
        const string html = """
            <script id="__NEXT_DATA__" type="application/json">
            {"props":{"pageProps":{"data":{"searchAds":{"items":[
              {"slug":"no-id","title":"Bez ID","totalPrice":{"value":1},"areaInSquareMeters":1},
              {"id":1,"title":"Bez sluga","totalPrice":{"value":1},"areaInSquareMeters":1},
              {"id":2,"slug":"no-area","title":"Bez powierzchni","totalPrice":{"value":1}},
              {"id":3,"slug":"ok","title":"OK","totalPrice":{"value":500000},"areaInSquareMeters":80}
            ]}}}}}
            </script>
            """;

        var listings = OtodomScraper.ParseNextData(html);

        Assert.That(listings, Has.Count.EqualTo(1));
        Assert.That(listings[0].ExternalId, Is.EqualTo("3"));
    }
}
