using System.Diagnostics;
using System.Net;
using Polly;
using ReceptyOks.Api.Extensions;

namespace ReceptyOks_UnitTests.HomeSeeker;

/// <summary>
/// Tests for the resilience pipeline used by the Nominatim and Deweloperuch HTTP clients.
/// </summary>
public sealed class PoliteApiPipelineTests
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(300);

    [TestCase(HttpStatusCode.TooManyRequests)]
    [TestCase(HttpStatusCode.Forbidden)]
    [TestCase(HttpStatusCode.Unauthorized)]
    public async Task Refusal_IsNotRetried(HttpStatusCode status)
    {
        var attempts = 0;

        using var response = await Build().ExecuteAsync(_ =>
        {
            attempts++;
            return ValueTask.FromResult(new HttpResponseMessage(status));
        });

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(status));
            Assert.That(attempts, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task TransientFailure_IsRetriedOnce()
    {
        var attempts = 0;

        using var response = await Build().ExecuteAsync(_ =>
        {
            attempts++;
            return ValueTask.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        });

        Assert.That(attempts, Is.EqualTo(2));
    }

    [Test]
    public async Task ConsecutiveRequests_AreSpacedByInterval()
    {
        var pipeline = Build();
        var stopwatch = Stopwatch.StartNew();

        for (var i = 0; i < 3; i++)
        {
            using var response = await pipeline.ExecuteAsync(_ => ValueTask.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        }

        // Two waits of at least 0.9 × interval (sliding window granularity).
        Assert.That(stopwatch.Elapsed, Is.GreaterThanOrEqualTo(Interval * 1.8 - TimeSpan.FromMilliseconds(20)));
    }

    private static ResiliencePipeline<HttpResponseMessage> Build()
    {
        var builder = new ResiliencePipelineBuilder<HttpResponseMessage>();
        HomeSeekerExtensions.AddPoliteApiPipeline(builder, Interval);
        return builder.Build();
    }
}
