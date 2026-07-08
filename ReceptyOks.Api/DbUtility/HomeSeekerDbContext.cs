using HomeSeeker.Models;
using Microsoft.EntityFrameworkCore;

namespace ReceptyOks.Api.DbUtility;

/// <summary>
/// Separate DbContext for HomeSeeker entities.
/// Uses the same database as RecipeDbContext but manages its own tables.
/// </summary>
public class HomeSeekerDbContext(DbContextOptions<HomeSeekerDbContext> options) : DbContext(options)
{
    public DbSet<SearchProfile> SearchProfiles => Set<SearchProfile>();
    public DbSet<HouseListing> HouseListings => Set<HouseListing>();
    public DbSet<ScanRun> ScanRuns => Set<ScanRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var isSqlServer = Database.IsSqlServer();

        // SearchProfile
        modelBuilder.Entity<SearchProfile>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.City).IsRequired().HasMaxLength(100);
            entity.Property(p => p.District).HasMaxLength(100);
            entity.Property(p => p.NotificationEmail).IsRequired().HasMaxLength(200);
            entity.Property(p => p.ExtraCriteria).HasMaxLength(2000);

            // Decimal precision for prices/areas
            entity.Property(p => p.MinPrice).HasPrecision(18, 2);
            entity.Property(p => p.MaxPrice).HasPrecision(18, 2);
            entity.Property(p => p.MinAreaSqm).HasPrecision(10, 2);
            entity.Property(p => p.MaxAreaSqm).HasPrecision(10, 2);

            entity.HasIndex(p => p.UpdatedAt);
            entity.HasIndex(p => p.IsDeleted);
            entity.HasIndex(p => p.IsActive);
            entity.HasIndex(p => p.LastScannedAt);
        });

        // HouseListing
        modelBuilder.Entity<HouseListing>(entity =>
        {
            entity.HasKey(l => l.Id);
            entity.Property(l => l.Portal).IsRequired().HasMaxLength(50);
            entity.Property(l => l.ExternalId).IsRequired().HasMaxLength(100);
            entity.Property(l => l.Url).IsRequired().HasMaxLength(1000);
            entity.Property(l => l.Title).IsRequired().HasMaxLength(500);
            entity.Property(l => l.Location).HasMaxLength(300);

            // Decimal precision
            entity.Property(l => l.Price).HasPrecision(18, 2);
            entity.Property(l => l.PreviousPrice).HasPrecision(18, 2);
            entity.Property(l => l.AreaSqm).HasPrecision(10, 2);

            // AI fields
            entity.Property(l => l.AiSummary).HasMaxLength(2000);
            entity.Property(l => l.AiProsJson).HasMaxLength(4000);
            entity.Property(l => l.AiConsJson).HasMaxLength(4000);
            entity.Property(l => l.AiPriceAssessment).HasMaxLength(100);

            // Unique index for deduplication: (SearchProfileId, Portal, ExternalId)
            entity.HasIndex(l => new { l.SearchProfileId, l.Portal, l.ExternalId })
                .IsUnique();

            entity.HasIndex(l => l.UpdatedAt);
            entity.HasIndex(l => l.IsDeleted);
            entity.HasIndex(l => l.AiScore);
            entity.HasIndex(l => l.FirstSeenAt);

            // Cascade delete when profile is deleted
            entity.HasOne(l => l.SearchProfile)
                .WithMany(p => p.Listings)
                .HasForeignKey(l => l.SearchProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ScanRun
        modelBuilder.Entity<ScanRun>(entity =>
        {
            entity.HasKey(s => s.Id);

            // Large HTML report - use appropriate column type
            if (isSqlServer)
            {
                entity.Property(s => s.ReportHtml).HasColumnType("NVARCHAR(MAX)");
            }
            else
            {
                // SQLite handles TEXT automatically
                entity.Property(s => s.ReportHtml);
            }

            entity.Property(s => s.Error).HasMaxLength(2000);

            entity.HasIndex(s => s.StartedAt);
            entity.HasIndex(s => s.Status);

            // Cascade delete when profile is deleted
            entity.HasOne(s => s.SearchProfile)
                .WithMany(p => p.ScanRuns)
                .HasForeignKey(s => s.SearchProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
