namespace ReceptyOks.Api.Endpoints;

public static class CategoryEndpoints
{
    public static void MapCategoryEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/categories")
            .WithTags("Categories")
            .RequireRateLimiting("fixed");

        // GET - wszystkie kategorie
        group.MapGet("/", async (RecipeDbContext db) =>
        {
            var categories = await db.Categories
                .Where(c => !c.IsDeleted)
                .OrderBy(c => c.Name)
                .ToListAsync();
            return Results.Ok(categories);
        })
        .WithName("GetAllCategories");

        // GET - pojedyncza kategoria
        group.MapGet("/{id:guid}", async (Guid id, RecipeDbContext db) =>
        {
            var category = await db.Categories.FindAsync(id);
            return category is null ? Results.NotFound() : Results.Ok(category);
        })
        .WithName("GetCategoryById");

        // GET - przepisy w kategorii
        group.MapGet("/{id:guid}/recipes", async (Guid id, RecipeDbContext db) =>
        {
            var recipes = await db.Recipes
                .Where(r => r.CategoryId == id && !r.IsDeleted)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
            return Results.Ok(recipes);
        })
        .WithName("GetRecipesByCategory");

        // POST - nowa kategoria
        group.MapPost("/", async (Category category, RecipeDbContext db) =>
        {
            category.Id = category.Id == Guid.Empty ? Guid.NewGuid() : category.Id;
            category.CreatedAt = DateTime.UtcNow;
            category.UpdatedAt = DateTime.UtcNow;

            db.Categories.Add(category);
            await db.SaveChangesAsync();

            return Results.Created($"/api/categories/{category.Id}", category);
        })
        .WithName("CreateCategory");

        // PUT - aktualizacja kategorii
        group.MapPut("/{id:guid}", async (Guid id, Category updatedCategory, RecipeDbContext db) =>
        {
            var category = await db.Categories.FindAsync(id);
            if (category is null)
                return Results.NotFound();

            category.Name = updatedCategory.Name;
            category.Description = updatedCategory.Description;
            category.IconName = updatedCategory.IconName;
            category.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();
            return Results.Ok(category);
        })
        .WithName("UpdateCategory");

        // DELETE - soft delete
        group.MapDelete("/{id:guid}", async (Guid id, RecipeDbContext db) =>
        {
            var category = await db.Categories.FindAsync(id);
            if (category is null)
                return Results.NotFound();

            category.IsDeleted = true;
            category.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return Results.NoContent();
        })
        .WithName("DeleteCategory");
    }
}
