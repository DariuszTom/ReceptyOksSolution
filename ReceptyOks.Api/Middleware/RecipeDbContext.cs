using Microsoft.EntityFrameworkCore;
using ReceptyOks.Shared.Models;

namespace ReceptyOks.Api.Middleware;

public class RecipeDbContext : DbContext
{
    public RecipeDbContext(DbContextOptions<RecipeDbContext> options)
        : base(options) { }

    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
    public DbSet<ShoppingListItem> ShoppingListItems => Set<ShoppingListItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Recipe
        modelBuilder.Entity<Recipe>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Title).IsRequired().HasMaxLength(200);
            entity.Property(r => r.Image).HasColumnType("BLOB");
            entity.Property(r => r.ImageContentType).HasMaxLength(50);

            entity.HasOne(r => r.Category)
                  .WithMany(c => c.Recipes)
                  .HasForeignKey(r => r.CategoryId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(r => r.UpdatedAt);
            entity.HasIndex(r => r.IsDeleted);
        });

        // Category
        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Name).IsRequired().HasMaxLength(100);
            entity.HasIndex(c => c.UpdatedAt);
        });

        // Ingredient
        modelBuilder.Entity<Ingredient>(entity =>
        {
            entity.HasKey(i => i.Id);
            entity.Property(i => i.Name).IsRequired().HasMaxLength(100);
            entity.HasIndex(i => i.UpdatedAt);
        });

        // RecipeIngredient (many-to-many)
        modelBuilder.Entity<RecipeIngredient>(entity =>
        {
            entity.HasKey(ri => ri.Id);

            entity.HasOne(ri => ri.Recipe)
                .WithMany(r => r.Ingredients)
                .HasForeignKey(ri => ri.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ri => ri.Ingredient)
                .WithMany(i => i.RecipeIngredients)
                .HasForeignKey(ri => ri.IngredientId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(ri => new { ri.RecipeId, ri.IngredientId });
        });

        // ShoppingListItem
        modelBuilder.Entity<ShoppingListItem>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Name).IsRequired().HasMaxLength(200);
            entity.Property(s => s.Unit).HasMaxLength(50);
            entity.Property(s => s.BoughtBy).HasMaxLength(100);
            entity.Property(s => s.Note).HasMaxLength(500);

            entity.HasOne(s => s.Ingredient)
                .WithMany()
                .HasForeignKey(s => s.IngredientId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(s => s.Recipe)
                .WithMany()
                .HasForeignKey(s => s.RecipeId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(s => s.IsBought);
            entity.HasIndex(s => s.UpdatedAt);
            entity.HasIndex(s => s.IsDeleted);
        });
    }
}
