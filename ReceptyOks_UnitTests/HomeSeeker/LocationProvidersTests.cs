using System.Net;
using System.Text;
using HomeSeeker.Configuration;
using HomeSeeker.Models;
using HomeSeeker.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ReceptyOks_UnitTests.HomeSeeker;

/// <summary>
/// Tests for the city-centre (Nominatim) and RCN valuation (Deweloperuch) providers.
/// </summary>
public sealed class LocationProvidersTests
{
    private static readonly GeoPoint Krakow = new(50.035954, 19.936972);

    [Test]
    public void Nominatim_ParseSearchResponse_ReadsStringCoordinates()
    {
        var point = NominatimCityCenterProvider.ParseSearchResponse(
            """[{"lat":"50.0619474","lon":"19.9368564","display_name":"Kraków"}]""");

        Assert.That(point, Is.EqualTo(new GeoPoint(50.0619474, 19.9368564)));
    }

    [TestCase("[]")]
    [TestCase("{broken")]
    [TestCase("""[{"lat":"x","lon":"19.9"}]""")]
    public void Nominatim_ParseSearchResponse_InvalidInput_ReturnsNull(string json)
    {
        Assert.That(NominatimCityCenterProvider.ParseSearchResponse(json), Is.Null);
    }

    [Test]
    public async Task Nominatim_CachesResultPerCity()
    {
        var handler = new StubHandler(_ => Json("""[{"lat":"50.06","lon":"19.94"}]"""));
        var sut = new NominatimCityCenterProvider(Factory(handler), NullLogger<NominatimCityCenterProvider>.Instance);

        var first = await sut.GetCenterAsync("Kraków");
        var second = await sut.GetCenterAsync("kraków");

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(new GeoPoint(50.06, 19.94)));
            Assert.That(second, Is.EqualTo(first));
            Assert.That(handler.Requests, Has.Count.EqualTo(1));
            Assert.That(handler.Requests[0].Query, Does.Contain("city=Krak%C3%B3w"));
        });
    }

    [Test]
    public async Task Deweloperuch_ParsesSummaryAndIgnoresTransactions()
    {
        var handler = new StubHandler(_ => Json("""
            {"estimatedValue":1412283,"estimatedPricePerSqm":17877.4,"radiusKm":0.5,"periodMonths":24,
             "stats":{"averagePricePerSqm":18000,"medianPricePerSqm":18365,"count":34},
             "transactions":[{"price":1}],"radiusPool":[{"x":1}]}
            """));
        var sut = CreateValuation(handler);

        var result = await sut.GetApartmentValuationAsync(Krakow, 79.4m);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(new RcnValuation(17877m, 18365m, 34, 0.5, 24)));
            Assert.That(handler.Requests.Single().Query, Is.EqualTo("?area=79&lat=50.035954&lng=19.936972"));
        });
    }

    [Test]
    public async Task Deweloperuch_CachesSameRequest()
    {
        var handler = new StubHandler(_ => Json("""{"estimatedPricePerSqm":15000}"""));
        var sut = CreateValuation(handler);

        await sut.GetApartmentValuationAsync(Krakow, 50);
        await sut.GetApartmentValuationAsync(Krakow, 50);

        Assert.That(handler.Requests, Has.Count.EqualTo(1));
    }

    [TestCase(HttpStatusCode.Forbidden)]
    [TestCase(HttpStatusCode.TooManyRequests)]
    [TestCase(HttpStatusCode.Unauthorized)]
    public async Task Deweloperuch_WhenAccessRefused_StopsCalling(HttpStatusCode status)
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(status));
        var sut = CreateValuation(handler);

        var first = await sut.GetApartmentValuationAsync(Krakow, 50);
        var second = await sut.GetApartmentValuationAsync(new GeoPoint(50.06, 19.94), 60);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.Null);
            Assert.That(second, Is.Null);
            Assert.That(handler.Requests, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task Deweloperuch_NotFound_ReturnsNull()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        Assert.That(await CreateValuation(handler).GetApartmentValuationAsync(Krakow, 50), Is.Null);
    }

    [Test]
    public async Task Deweloperuch_DisabledOrOutOfRange_DoesNotCall()
    {
        var handler = new StubHandler(_ => Json("""{"estimatedPricePerSqm":15000}"""));

        var disabled = await CreateValuation(handler, o => o.UseRcnValuation = false).GetApartmentValuationAsync(Krakow, 50);
        var abroad = await CreateValuation(handler).GetApartmentValuationAsync(new GeoPoint(48.2, 16.37), 50);
        var tiny = await CreateValuation(handler).GetApartmentValuationAsync(Krakow, 5);

        Assert.Multiple(() =>
        {
            Assert.That(disabled, Is.Null);
            Assert.That(abroad, Is.Null);
            Assert.That(tiny, Is.Null);
            Assert.That(handler.Requests, Is.Empty);
        });
    }

    [TestCase("[]", true)]
    [TestCase("{broken", false)]
    [TestCase("""{"error":"x"}""", false)]
    [TestCase("""[{"lat":"x","lon":"19.9"}]""", false)]
    [TestCase("""[{"lat":"50.06","lon":"19.94"}]""", true)]
    public void Nominatim_TryParseSearchResponse_DistinguishesDefinitiveResults(string json, bool definitive)
    {
        Assert.That(NominatimCityCenterProvider.TryParseSearchResponse(json, out _), Is.EqualTo(definitive));
    }

    [Test]
    public async Task Nominatim_MalformedResponse_IsNotCached()
    {
        var responses = new Queue<string>(["{broken", """[{"lat":"50.06","lon":"19.94"}]"""]);
        var handler = new StubHandler(_ => Json(responses.Dequeue()));
        var sut = new NominatimCityCenterProvider(Factory(handler), NullLogger<NominatimCityCenterProvider>.Instance);

        var first = await sut.GetCenterAsync("Kraków");
        var second = await sut.GetCenterAsync("Kraków");

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.Null);
            Assert.That(second, Is.EqualTo(new GeoPoint(50.06, 19.94)));
            Assert.That(handler.Requests, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public async Task Nominatim_EmptyResult_IsCached()
    {
        var handler = new StubHandler(_ => Json("[]"));
        var sut = new NominatimCityCenterProvider(Factory(handler), NullLogger<NominatimCityCenterProvider>.Instance);

        await sut.GetCenterAsync("Nibylandia");
        await sut.GetCenterAsync("Nibylandia");

        Assert.That(handler.Requests, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Deweloperuch_MalformedResponse_IsNotCached()
    {
        var responses = new Queue<string>(["{broken", """{"estimatedPricePerSqm":15000}"""]);
        var handler = new StubHandler(_ => Json(responses.Dequeue()));
        var sut = CreateValuation(handler);

        var first = await sut.GetApartmentValuationAsync(Krakow, 50);
        var second = await sut.GetApartmentValuationAsync(Krakow, 50);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.Null);
            Assert.That(second?.EstimatedPricePerSqm, Is.EqualTo(15000m));
            Assert.That(handler.Requests, Has.Count.EqualTo(2));
        });
    }

    [Test, CancelAfter(10_000)]
    public async Task Deweloperuch_StalledBody_TimesOutAndReturnsNull()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new StalledStream()) });
        var sut = new DeweloperuchValuationProvider(
            Factory(handler), Options.Create(new HomeSeekerOptions()), NullLogger<DeweloperuchValuationProvider>.Instance)
        {
            BodyReadTimeout = TimeSpan.FromMilliseconds(100)
        };

        Assert.That(await sut.GetApartmentValuationAsync(Krakow, 50), Is.Null);
    }

    private static DeweloperuchValuationProvider CreateValuation(StubHandler handler, Action<HomeSeekerOptions>? configure = null)
    {
        var options = new HomeSeekerOptions();
        configure?.Invoke(options);
        return new DeweloperuchValuationProvider(
            Factory(handler), Options.Create(options), NullLogger<DeweloperuchValuationProvider>.Instance);
    }

    /// <summary>A response body that never delivers data until cancelled.</summary>
    private sealed class StalledStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => 0; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    private static IHttpClientFactory Factory(HttpMessageHandler handler)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler, disposeHandler: false));
        return factory.Object;
    }

    private static HttpResponseMessage Json(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            return Task.FromResult(respond(request));
        }
    }
}
