using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using ReceptyOks.Api.Middleware;
using ReceptyOks.Shared.Configuration;
using ReceptyOks.Shared.Models;

namespace ReceptyOks_UnitTests.Services;

[TestFixture]
public class ShopingListCleanerTests
{
    private ServiceProvider _serviceProvider = null!;
    private string _dbPath = null!;

    [SetUp]
    public void SetUp()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"cleaner_test_{Guid.NewGuid():N}.db");

        var services = new ServiceCollection();
        services.AddDbContext<RecipeDbContext>(opts =>
            opts.UseSqlite($"DataSource={_dbPath}"));
        _serviceProvider = services.BuildServiceProvider();

        var db = _serviceProvider.GetRequiredService<RecipeDbContext>();
        db.Database.EnsureCreated();
    }

    [TearDown]
    public void TearDown()
    {
        _serviceProvider.Dispose();
    }

    [Test]
    public async Task WhenBoughtItemIsOlderThanMaxAgeThenItIsDeleted()
    {
        var db = _serviceProvider.GetRequiredService<RecipeDbContext>();

        var options = new CleanupOptions { MaxAge = TimeSpan.FromDays(7), StartupDelay = TimeSpan.Zero };

        db.ShoppingListItems.Add(new ShoppingListItem
        {
            Id = Guid.NewGuid(),
            Name = "Old milk",
            IsBought = true,
            BoughtAt = DateTime.UtcNow.AddDays(-10),
            CreatedAt = DateTime.UtcNow.AddDays(-15),
            UpdatedAt = DateTime.UtcNow.AddDays(-10)
        });
        await db.SaveChangesAsync();

        var sut = CreateService(options);

        await sut.StartAsync(CancellationToken.None);
        // Give ExecuteAsync time to run the initial cleanup.
        await Task.Delay(200);
        await sut.StopAsync(CancellationToken.None);

        var remaining = await db.ShoppingListItems.CountAsync();
        Assert.That(remaining, Is.EqualTo(0));
    }

    [Test]
    public async Task WhenBoughtItemIsNewerThanMaxAgeThenItIsKept()
    {
        var db = _serviceProvider.GetRequiredService<RecipeDbContext>();

        var options = new CleanupOptions { MaxAge = TimeSpan.FromDays(7), StartupDelay = TimeSpan.Zero };

        db.ShoppingListItems.Add(new ShoppingListItem
        {
            Id = Guid.NewGuid(),
            Name = "Fresh bread",
            IsBought = true,
            BoughtAt = DateTime.UtcNow.AddDays(-3),
            CreatedAt = DateTime.UtcNow.AddDays(-5),
            UpdatedAt = DateTime.UtcNow.AddDays(-3)
        });
        await db.SaveChangesAsync();

        var sut = CreateService(options);

        await sut.StartAsync(CancellationToken.None);
        await Task.Delay(200);
        await sut.StopAsync(CancellationToken.None);

        var remaining = await db.ShoppingListItems.CountAsync();
        Assert.That(remaining, Is.EqualTo(1));
    }

    [Test]
    public async Task WhenItemIsNotBoughtThenItIsKept()
    {
        var db = _serviceProvider.GetRequiredService<RecipeDbContext>();

        var options = new CleanupOptions { MaxAge = TimeSpan.FromDays(7), StartupDelay = TimeSpan.Zero };

        db.ShoppingListItems.Add(new ShoppingListItem
        {
            Id = Guid.NewGuid(),
            Name = "Eggs",
            IsBought = false,
            BoughtAt = null,
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            UpdatedAt = DateTime.UtcNow.AddDays(-30)
        });
        await db.SaveChangesAsync();

        var sut = CreateService(options);

        await sut.StartAsync(CancellationToken.None);
        await Task.Delay(200);
        await sut.StopAsync(CancellationToken.None);

        var remaining = await db.ShoppingListItems.CountAsync();
        Assert.That(remaining, Is.EqualTo(1));
    }

    [Test]
    public async Task WhenCancelledDuringStartupDelayThenStopsPromptly()
    {
        var options = new CleanupOptions
        {
            StartupDelay = TimeSpan.FromHours(1),
            Interval = TimeSpan.FromHours(1),
            MaxAge = TimeSpan.FromDays(7)
        };

        var sut = CreateService(options);

        using var cts = new CancellationTokenSource();

        await sut.StartAsync(cts.Token);
        await cts.CancelAsync();
        await sut.StopAsync(CancellationToken.None);

        Assert.Pass();
    }

    private ShoppingListCleaner CreateService(CleanupOptions options) =>
        new(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            options,
            Mock.Of<ILogger<ShoppingListCleaner>>());
}
