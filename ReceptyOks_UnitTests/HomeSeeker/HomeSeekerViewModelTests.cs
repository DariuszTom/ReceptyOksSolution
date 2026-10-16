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
        _service.Setup(s => s.GetListingsAsync(1, It.IsAny<int>(), It.IsAny<HomeSeekerListingQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Page(1, totalPages: 3, firstPageCalls++ == 0 ? "old-1" : "new-1"));
        _service.Setup(s => s.GetListingsAsync(2, It.IsAny<int>(), It.IsAny<HomeSeekerListingQuery>(), It.IsAny<CancellationToken>()))
            .Returns(page2.Task);

        var vm = CreateViewModel();
        await vm.LoadCommand.ExecuteAsync(null);

        var loadMore = vm.LoadMoreListingsCommand.ExecuteAsync(null);
        await vm.LoadListingsCommand.ExecuteAsync(null);
        page2.SetResult(Page(2, totalPages: 3, "old-2"));
        await loadMore;

        Assert.That(vm.Listings.Select(l => l.Title), Is.EqualTo(new[] { "new-1" }));

        // Pagination state still points at the refreshed first page.
        _service.Setup(s => s.GetListingsAsync(2, It.IsAny<int>(), It.IsAny<HomeSeekerListingQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Page(2, totalPages: 3, "new-2"));
        await vm.LoadMoreListingsCommand.ExecuteAsync(null);
        Assert.That(vm.Listings.Select(l => l.Title), Is.EqualTo(new[] { "new-1", "new-2" }));
    }

    [Test]
    public async Task OlderReload_CompletingAfterNewerReload_IsDiscarded()
    {
        var slow = new TaskCompletionSource<HomeSeekerResult<HomeSeekerPagedResult<HomeSeekerListing>>>();
        _service.Setup(s => s.GetListingsAsync(1, It.IsAny<int>(), It.Is<HomeSeekerListingQuery>(q => q.Sort == null), It.IsAny<CancellationToken>()))
            .Returns(slow.Task);
        _service.Setup(s => s.GetListingsAsync(1, It.IsAny<int>(), It.Is<HomeSeekerListingQuery>(q => q.Sort == "price"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Page(1, totalPages: 1, "by-price"));

        var vm = CreateViewModel();
        var refresh = vm.LoadListingsCommand.ExecuteAsync(null);
        vm.SelectedSortOption = "Cena";
        await Task.Yield();
        slow.SetResult(Page(1, totalPages: 1, "by-score"));
        await refresh;

        Assert.That(vm.Listings.Select(l => l.Title), Is.EqualTo(new[] { "by-price" }));
        Assert.That(vm.IsRefreshingListings, Is.False);
    }

    [Test]
    public async Task FilterChangesDuringReplay_AreAllApplied()
    {
        var initial = new TaskCompletionSource<HomeSeekerResult<HomeSeekerPagedResult<HomeSeekerListing>>>();
        var replay = new TaskCompletionSource<HomeSeekerResult<HomeSeekerPagedResult<HomeSeekerListing>>>();
        _service.Setup(s => s.GetListingsAsync(1, It.IsAny<int>(), It.Is<HomeSeekerListingQuery>(q => q.Sort == null), It.IsAny<CancellationToken>()))
            .Returns(initial.Task);
        _service.Setup(s => s.GetListingsAsync(1, It.IsAny<int>(), It.Is<HomeSeekerListingQuery>(q => q.Sort == "price"), It.IsAny<CancellationToken>()))
            .Returns(replay.Task);
        _service.Setup(s => s.GetListingsAsync(1, It.IsAny<int>(), It.Is<HomeSeekerListingQuery>(q => q.Sort == "firstseen"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Page(1, totalPages: 1, "newest"));

        var vm = CreateViewModel();
        var load = vm.LoadCommand.ExecuteAsync(null);
        vm.SelectedSortOption = "Cena";
        initial.SetResult(Page(1, totalPages: 1, "by-score"));
        vm.SelectedSortOption = "Najnowsze";
        replay.SetResult(Page(1, totalPages: 1, "by-price"));
        await load;

        Assert.That(vm.Listings.Select(l => l.Title), Is.EqualTo(new[] { "newest" }));
    }

    [Test]
    public async Task FilterOptions_AreMappedToQuery_AndCounted()
    {
        HomeSeekerListingQuery? lastQuery = null;
        _service.Setup(s => s.GetListingsAsync(1, It.IsAny<int>(), It.IsAny<HomeSeekerListingQuery>(), It.IsAny<CancellationToken>()))
            .Callback<int, int, HomeSeekerListingQuery, CancellationToken>((_, _, q, _) => lastQuery = q)
            .ReturnsAsync(Page(1, totalPages: 1, "x"));

        var vm = CreateViewModel();
        await vm.LoadCommand.ExecuteAsync(null);
        Assert.That(vm.FiltersHeaderText, Is.EqualTo("Filtry"));

        vm.SelectedSortOption = "Najbliżej centrum";
        vm.SelectedPropertyTypeOption = "Mieszkania";
        vm.SelectedPortalOption = "OLX";
        vm.SelectedMaxDistanceOption = "≤ 5 km od centrum";
        vm.PriceDropOnly = true;
        await vm.LoadListingsCommand.ExecuteAsync(null);

        Assert.That(lastQuery, Is.EqualTo(new HomeSeekerListingQuery
        {
            Sort = "distance",
            PropertyType = HomeSeekerPropertyTypes.Apartment,
            Portal = "OLX",
            MaxDistanceKm = 5,
            PriceDropOnly = true
        }));
        Assert.That(vm.ActiveFiltersCount, Is.EqualTo(4));
        Assert.That(vm.FiltersHeaderText, Is.EqualTo("Filtry (4)"));

        vm.ClearFiltersCommand.Execute(null);
        await vm.LoadListingsCommand.ExecuteAsync(null);

        Assert.That(lastQuery, Is.EqualTo(new HomeSeekerListingQuery { Sort = "distance" }));
        Assert.That(vm.ActiveFiltersCount, Is.Zero);
    }

    [Test]
    public async Task SupersededReload_CancellationIsSwallowed()
    {
        _service.Setup(s => s.GetListingsAsync(1, It.IsAny<int>(), It.Is<HomeSeekerListingQuery>(q => q.Sort == null), It.IsAny<CancellationToken>()))
            .Returns<int, int, HomeSeekerListingQuery, CancellationToken>(async (_, _, _, ct) =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                return Page(1, 1, "never");
            });
        _service.Setup(s => s.GetListingsAsync(1, It.IsAny<int>(), It.Is<HomeSeekerListingQuery>(q => q.Sort == "price"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Page(1, totalPages: 1, "by-price"));

        var vm = CreateViewModel();
        var first = vm.LoadListingsCommand.ExecuteAsync(null);
        vm.SelectedSortOption = "Cena";

        Assert.DoesNotThrowAsync(async () => await first);
        Assert.That(vm.Listings.Select(l => l.Title), Is.EqualTo(new[] { "by-price" }));
    }

    [Test]
    public async Task FailedReload_DisablesLoadMoreForOldResults()
    {
        _service.Setup(s => s.GetListingsAsync(1, It.IsAny<int>(), It.Is<HomeSeekerListingQuery>(q => q.Sort == null), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Page(1, totalPages: 3, "old-1"));
        _service.Setup(s => s.GetListingsAsync(1, It.IsAny<int>(), It.Is<HomeSeekerListingQuery>(q => q.Sort == "price"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(HomeSeekerResult<HomeSeekerPagedResult<HomeSeekerListing>>.Fail(HomeSeekerCallStatus.Error, "boom"));

        var vm = CreateViewModel();
        await vm.LoadCommand.ExecuteAsync(null);
        vm.SelectedSortOption = "Cena";
        await vm.LoadListingsCommand.ExecuteAsync(null);
        await vm.LoadMoreListingsCommand.ExecuteAsync(null);

        _service.Verify(s => s.GetListingsAsync(2, It.IsAny<int>(), It.IsAny<HomeSeekerListingQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task FilterChangeDuringInitialLoad_ReloadsWithLatestFilter()
    {
        var slow = new TaskCompletionSource<HomeSeekerResult<HomeSeekerPagedResult<HomeSeekerListing>>>();
        _service.Setup(s => s.GetListingsAsync(1, It.IsAny<int>(), It.Is<HomeSeekerListingQuery>(q => q.Sort == null), It.IsAny<CancellationToken>()))
            .Returns(slow.Task);
        _service.Setup(s => s.GetListingsAsync(1, It.IsAny<int>(), It.Is<HomeSeekerListingQuery>(q => q.Sort == "price"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Page(1, totalPages: 1, "by-price"));

        var vm = CreateViewModel();
        var load = vm.LoadCommand.ExecuteAsync(null);
        vm.SelectedSortOption = "Cena";
        slow.SetResult(Page(1, totalPages: 1, "by-score"));
        await load;

        Assert.That(vm.Listings.Select(l => l.Title), Is.EqualTo(new[] { "by-price" }));
        Assert.That(vm.IsLoading, Is.False);
    }

    [Test]
    public async Task PullToRefreshFinishingBeforeFullLoad_ReleasesSpinnerWhenLoadCompletes()
    {
        var slow = new TaskCompletionSource<HomeSeekerResult<HomeSeekerPagedResult<HomeSeekerListing>>>();
        var calls = 0;
        _service.Setup(s => s.GetListingsAsync(1, It.IsAny<int>(), It.IsAny<HomeSeekerListingQuery>(), It.IsAny<CancellationToken>()))
            .Returns(() => calls++ == 0 ? slow.Task : Task.FromResult(Page(1, totalPages: 1, "refreshed")));

        var vm = CreateViewModel();
        var load = vm.LoadCommand.ExecuteAsync(null);

        // RefreshView sets the flag on pull, then runs the command.
        vm.IsRefreshingListings = true;
        await vm.LoadListingsCommand.ExecuteAsync(null);
        Assert.That(vm.IsRefreshingListings, Is.True, "full load still pending");

        slow.SetResult(Page(1, totalPages: 1, "initial"));
        await load;

        Assert.That(vm.IsRefreshingListings, Is.False);
        Assert.That(vm.Listings.Select(l => l.Title), Is.EqualTo(new[] { "refreshed" }));
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
