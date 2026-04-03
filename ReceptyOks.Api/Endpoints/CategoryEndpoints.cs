using FluentValidation;
using ReceptyOks.Shared.Interfaces;

namespace ReceptyOks.Api.Endpoints;

public static class CategoryEndpoints
{
    public static void MapCategoryEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/categories")
            .WithTags("Categories")
            .RequireRateLimiting("fixed");

        // GET - all categories
        group.MapGet("/", async (RecipeDbContext db) =>
        {
            var categories = await db.Categories
                .AsNoTracking()
                .Where(c => !c.IsDeleted)
                .OrderBy(c => c.Name)
                .ToListAsync().ConfigureAwait(false);
            return Results.Ok(categories);
        })
        .WithName("GetAllCategories");

        // GET - single category
        group.MapGet("/{id:guid}", async (Guid id, RecipeDbContext db) =>
        {
            var category = await db.Categories.FindAsync(id).ConfigureAwait(false);
            return category is null ? Results.NotFound() : Results.Ok(category);
        })
        .WithName("GetCategoryById");

        // GET - recipes in category
        group.MapGet("/{id:guid}/recipes", async (Guid id, RecipeDbContext db) =>
        {
            var recipes = await db.Recipes
                .AsNoTracking()
                .Where(r => r.CategoryId == id && !r.IsDeleted)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync().ConfigureAwait(false);
            return Results.Ok(recipes);
        })
        .WithName("GetRecipesByCategory");

        // POST - create new category
        group.MapPost("/", async (Category category, IValidator<ICategoryData> validator, RecipeDbContext db) =>
        {
            var validationResult = await validator.ValidateAsync(category).ConfigureAwait(false);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            category.Id = category.Id == Guid.Empty ? Guid.NewGuid() : category.Id;
            category.CreatedAt = DateTime.UtcNow;
            category.UpdatedAt = DateTime.UtcNow;

            db.Categories.Add(category);
            await db.SaveChangesAsync().ConfigureAwait(false);

            return Results.Created($"/api/categories/{category.Id}", category);
        })
        .WithName("CreateCategory");

        // PUT - update category
        group.MapPut("/{id:guid}", async (Guid id, Category updatedCategory, IValidator<ICategoryData> validator, RecipeDbContext db) =>
        {
            var validationResult = await validator.ValidateAsync(updatedCategory).ConfigureAwait(false);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            var category = await db.Categories.FindAsync(id).ConfigureAwait(false);
            if (category is null)
                return Results.NotFound();

            category.Name = updatedCategory.Name;
            category.Description = updatedCategory.Description;
            category.IconName = updatedCategory.IconName;
            category.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync().ConfigureAwait(false);
            return Results.Ok(category);
        })
        .WithName("UpdateCategory");

        // DELETE - soft delete
        group.MapDelete("/{id:guid}", async (Guid id, RecipeDbContext db) =>
        {
            var category = await db.Categories.FindAsync(id).ConfigureAwait(false);
            if (category is null)
                return Results.NotFound();

            category.IsDeleted = true;
            category.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync().ConfigureAwait(false);

            return Results.NoContent();
        })
        .WithName("DeleteCategory");
    }
}
