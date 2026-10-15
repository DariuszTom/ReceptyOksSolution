using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReceptyOks.Models.HomeSeeker;
using ReceptyOks.Services;
using ReceptyOks.ViewModels;

namespace ReceptyOks_UnitTests.HomeSeeker;

/// <summary>Ordering guarantees between overlapping listing requests in <see cref="HomeSeekerViewModel"/>.</summary>
public sealed class HomeSeekerViewModelTests
{
    private Mock<IHomeSeekerService> _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new Mock<IHomeSeekerService>();
        _service.Setup(s => s.GetProfileAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(HomeSeekerResult<HomeSeekerProfile>.Ok(new HomeSeekerProfile { City = "Wrocław" }));
        _service.Setup(s => s.GetScansAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(HomeSeekerResult<List<HomeSeekerScanRun>>.Ok([]));
    }

    [Test]
    public async Task LoadMore_CompletingAfterReload_IsDiscarded()
    {
        var page2 = new TaskCompletionSource<HomeSeekerResult<HomeSeekerPagedResult<HomeSeekerListing>>>();
        var firstPageCalls = 0;
        _service.Setup(s => s.GetListingsAsync(1, It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Page(1, totalPages: 3, firstPageCalls++ == 0 ? "old-1" : "new-1"));
        _service.Setup(s => s.GetListingsAsync(2, It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(page2.Task);

        var vm = CreateViewModel();
        await vm.LoadCommand.ExecuteAsync(null);

        var loadMore = vm.LoadMoreListingsCommand.ExecuteAsync(null);
        await vm.LoadListingsCommand.ExecuteAsync(null);
        page2.SetResult(Page(2, totalPages: 3, "old-2"));
        await loadMore;

        Assert.That(vm.Listings.Select(l => l.Title), Is.EqualTo(new[] { "new-1" }));

        // Pagination state still points at the refreshed first page.
        _service.Setup(s => s.GetListingsAsync(2, It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Page(2, totalPages: 3, "new-2"));
        await vm.LoadMoreListingsCommand.ExecuteAsync(null);
        Assert.That(vm.Listings.Select(l => l.Title), Is.EqualTo(new[] { "new-1", "new-2" }));
    }

    [Test]
    public async Task OlderReload_CompletingAfterNewerReload_IsDiscarded()
    {
        var slow = new TaskCompletionSource<HomeSeekerResult<HomeSeekerPagedResult<HomeSeekerListing>>>();
        _service.Setup(s => s.GetListingsAsync(1, It.IsAny<int>(), It.IsAny<int?>(), null, It.IsAny<CancellationToken>()))
            .Returns(slow.Task);
        _service.Setup(s => s.GetListingsAsync(1, It.IsAny<int>(), It.IsAny<int?>(), "price", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Page(1, totalPages: 1, "by-price"));

        var vm = CreateViewModel();
        var refresh = vm.LoadListingsCommand.ExecuteAsync(null);
        vm.SelectedSortOption = "Cena";
        await Task.Yield();
        slow.SetResult(Page(1, totalPages: 1, "by-score"));
        await refresh;

        Assert.That(vm.Listings.Select(l => l.Title), Is.EqualTo(new[] { "by-price" }));
        Assert.That(vm.IsRefreshing, Is.False);
    }

    [Test]
    public async Task FilterChangeDuringInitialLoad_ReloadsWithLatestFilter()
    {
        var slow = new TaskCompletionSource<HomeSeekerResult<HomeSeekerPagedResult<HomeSeekerListing>>>();
        _service.Setup(s => s.GetListingsAsync(1, It.IsAny<int>(), It.IsAny<int?>(), null, It.IsAny<CancellationToken>()))
            .Returns(slow.Task);
        _service.Setup(s => s.GetListingsAsync(1, It.IsAny<int>(), It.IsAny<int?>(), "price", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Page(1, totalPages: 1, "by-price"));

        var vm = CreateViewModel();
        var load = vm.LoadCommand.ExecuteAsync(null);
        vm.SelectedSortOption = "Cena";
        slow.SetResult(Page(1, totalPages: 1, "by-score"));
        await load;

        Assert.That(vm.Listings.Select(l => l.Title), Is.EqualTo(new[] { "by-price" }));
        Assert.That(vm.IsLoading, Is.False);
    }

    private HomeSeekerViewModel CreateViewModel() => new(_service.Object, NullLogger<HomeSeekerViewModel>.Instance);

    private static HomeSeekerResult<HomeSeekerPagedResult<HomeSeekerListing>> Page(int page, int totalPages, params string[] titles) =>
        HomeSeekerResult<HomeSeekerPagedResult<HomeSeekerListing>>.Ok(new HomeSeekerPagedResult<HomeSeekerListing>
        {
            PageNumber = page,
            TotalPages = totalPages,
            TotalCount = totalPages * titles.Length,
            Data = titles.Select(t => new HomeSeekerListing { Id = Guid.NewGuid(), Title = t }).ToList()
        });
}
