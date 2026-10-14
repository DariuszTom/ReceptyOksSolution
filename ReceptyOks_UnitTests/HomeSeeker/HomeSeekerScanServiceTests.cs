using HomeSeeker.Abstractions;
using HomeSeeker.Configuration;
using HomeSeeker.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ReceptyOks.Api.Middleware;

namespace ReceptyOks_UnitTests.HomeSeeker;

/// <summary>
/// Tests for the <see cref="HomeSeekerScanService"/> scheduler: enable flag,
/// shared-profile checks and scan-lease handling.
/// </summary>
public sealed class HomeSeekerScanServiceTests
{
    private static readonly Guid ProfileId = Guid.NewGuid();

    private Mock<IListingRepository> _repository = null!;
    private Mock<IMarketScanService> _scanService = null!;
    private ServiceProvider _provider = null!;
    private SearchProfile? _profile;
    private TaskCompletionSource _released = null!;

    [SetUp]
    public void SetUp()
    {
        _profile = new SearchProfile { Id = ProfileId, City = "Wrocław", NotificationEmail = "a@example.com", IsActive = true };
        _released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _repository = new Mock<IListingRepository>();
        _repository.Setup(r => r.GetSharedProfileAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => _profile);
        _repository.Setup(r => r.ReleaseScanLeaseAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback(() => _released.TrySetResult())
            .ReturnsAsync(true);

        _scanService = new Mock<IMarketScanService>();

        var services = new ServiceCollection();
        services.AddScoped(_ => _repository.Object);
        services.AddScoped(_ => _scanService.Object);
        _provider = services.BuildServiceProvider();
    }

    [TearDown]
    public void TearDown() => _provider.Dispose();

    [Test]
    public async Task WhenDisabled_DoesNotTouchRepository()
    {
        await RunAsync(enabled: false);

        _repository.VerifyNoOtherCalls();
        _scanService.VerifyNoOtherCalls();
    }

    [Test]
    public async Task WhenProfileNotConfigured_SkipsScan()
    {
        _profile = null;

        await RunAsync();

        _repository.Verify(r => r.TryAcquireScanLeaseAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _scanService.VerifyNoOtherCalls();
    }

    [Test]
    public async Task WhenProfileInactive_SkipsScan()
    {
        _profile!.IsActive = false;

        await RunAsync();

        _repository.Verify(r => r.TryAcquireScanLeaseAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _scanService.VerifyNoOtherCalls();
    }

    [Test]
    public async Task WhenLeaseNotAcquired_DoesNotScanOrRelease()
    {
        _repository.Setup(r => r.TryAcquireScanLeaseAsync(ProfileId, It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);

        await RunAsync();

        _repository.Verify(r => r.TryAcquireScanLeaseAsync(ProfileId, It.IsAny<CancellationToken>()), Times.Once);
        _scanService.VerifyNoOtherCalls();
        _repository.Verify(r => r.ReleaseScanLeaseAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task WhenScanSucceeds_ReleasesLeaseAndMarksScanned()
    {
        var token = Guid.NewGuid();
        _repository.Setup(r => r.TryAcquireScanLeaseAsync(ProfileId, It.IsAny<CancellationToken>())).ReturnsAsync(token);
        _scanService.Setup(s => s.RunScanAsync(ProfileId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await RunUntilReleasedAsync();

        _repository.Verify(r => r.ReleaseScanLeaseAsync(ProfileId, token, true, CancellationToken.None), Times.Once);
    }

    [Test]
    public async Task WhenScanFails_ReleasesLeaseWithoutMarkingScanned()
    {
        var token = Guid.NewGuid();
        _repository.Setup(r => r.TryAcquireScanLeaseAsync(ProfileId, It.IsAny<CancellationToken>())).ReturnsAsync(token);
        _scanService.Setup(s => s.RunScanAsync(ProfileId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await RunUntilReleasedAsync();

        _repository.Verify(r => r.ReleaseScanLeaseAsync(ProfileId, token, false, CancellationToken.None), Times.Once);
    }

    [Test]
    public async Task WhenScanThrows_ReleasesLeaseWithoutMarkingScanned()
    {
        var token = Guid.NewGuid();
        _repository.Setup(r => r.TryAcquireScanLeaseAsync(ProfileId, It.IsAny<CancellationToken>())).ReturnsAsync(token);
        _scanService.Setup(s => s.RunScanAsync(ProfileId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        await RunUntilReleasedAsync();

        _repository.Verify(r => r.ReleaseScanLeaseAsync(ProfileId, token, false, CancellationToken.None), Times.Once);
    }

    [Test]
    public async Task WhenProfileLookupThrows_ServiceKeepsRunning()
    {
        _repository.Setup(r => r.GetSharedProfileAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));

        var sut = CreateSut(enabled: true);
        await sut.StartAsync(CancellationToken.None);
        await Task.Delay(200);

        Assert.That(sut.ExecuteTask?.IsFaulted, Is.False);
        await sut.StopAsync(CancellationToken.None);
    }

    [Test]
    public async Task WhenStoppedDuringStartupDelay_StopsPromptly()
    {
        var sut = CreateSut(enabled: true, startupDelay: TimeSpan.FromHours(1));

        await sut.StartAsync(CancellationToken.None);
        var stop = sut.StopAsync(CancellationToken.None);

        Assert.That(await Task.WhenAny(stop, Task.Delay(TimeSpan.FromSeconds(5))), Is.SameAs(stop));
        _repository.VerifyNoOtherCalls();
    }

    private async Task RunAsync(bool enabled = true)
    {
        var sut = CreateSut(enabled);
        await sut.StartAsync(CancellationToken.None);
        await Task.Delay(200);
        await sut.StopAsync(CancellationToken.None);
    }

    private async Task RunUntilReleasedAsync()
    {
        var sut = CreateSut(enabled: true);
        await sut.StartAsync(CancellationToken.None);
        var completed = await Task.WhenAny(_released.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        await sut.StopAsync(CancellationToken.None);

        Assert.That(completed, Is.SameAs(_released.Task), "lease was not released");
    }

    private HomeSeekerScanService CreateSut(bool enabled, TimeSpan? startupDelay = null) =>
        new(
            _provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new HomeSeekerOptions
            {
                Enabled = enabled,
                StartupDelay = startupDelay ?? TimeSpan.Zero,
                ScanInterval = TimeSpan.FromHours(1)
            }),
            NullLogger<HomeSeekerScanService>.Instance);
}
