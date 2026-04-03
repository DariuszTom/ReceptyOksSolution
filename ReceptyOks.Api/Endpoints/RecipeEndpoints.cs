namespace ReceptyOks.Api.Endpoints;

public static class RecipeEndpoints
{
    public static void MapRecipeEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/recipes")
            .WithTags("Recipes")
            .RequireRateLimiting("fixed");

        // GET - wszystkie przepisy (bez usuniêtych)
        group.MapGet("/", async (RecipeDbContext db) =>
        {
            var recipes = await db.Recipes
                .AsNoTracking()
                .Include(r => r.Category)
                .Include(r => r.Ingredients)
                    .ThenInclude(ri => ri.Ingredient)
                .Where(r => !r.IsDeleted)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync().ConfigureAwait(false);
            return Results.Ok(recipes);
        })
        .WithName("GetAllRecipes");

        // GET - single recipe
        group.MapGet("/{id:guid}", async (Guid id, RecipeDbContext db) =>
        {
            var recipe = await db.Recipes
                .AsNoTracking()
                .Include(r => r.Category)
                .Include(r => r.Ingredients)
                    .ThenInclude(ri => ri.Ingredient)
                .FirstOrDefaultAsync(r => r.Id == id).ConfigureAwait(false);

            return recipe is null ? Results.NotFound() : Results.Ok(recipe);
        })
        .WithName("GetRecipeById");

        // GET - recipe image
        group.MapGet("/{id:guid}/image", async (Guid id, RecipeDbContext db) =>
        {
            var recipe = await db.Recipes
                .Where(r => r.Id == id)
                .Select(r => new { r.Image, r.ImageContentType })
                .FirstOrDefaultAsync().ConfigureAwait(false);

            if (recipe?.Image is null)
                return Results.NotFound();

            return Results.File(recipe.Image, recipe.ImageContentType ?? "image/jpeg");
        })
        .WithName("GetRecipeImage");

        // POST - create new recipe
        group.MapPost("/", async (Recipe recipe, RecipeDbContext db) =>
        {
            recipe.Id = recipe.Id == Guid.Empty ? Guid.NewGuid() : recipe.Id;
            recipe.CreatedAt = DateTime.UtcNow;
            recipe.UpdatedAt = DateTime.UtcNow;

            db.Recipes.Add(recipe);
            await db.SaveChangesAsync().ConfigureAwait(false);

            return Results.Created($"/api/recipes/{recipe.Id}", recipe);
        })
        .WithName("CreateRecipe");

        // PUT - update recipe
        group.MapPut("/{id:guid}", async (Guid id, Recipe updatedRecipe, RecipeDbContext db) =>
        {
            var recipe = await db.Recipes
                .Include(r => r.Ingredients)
                .FirstOrDefaultAsync(r => r.Id == id).ConfigureAwait(false);

            if (recipe is null)
                return Results.NotFound();

            recipe.Title = updatedRecipe.Title;
            recipe.Description = updatedRecipe.Description;
            recipe.Instructions = updatedRecipe.Instructions;
            recipe.PreparationTimeMinutes = updatedRecipe.PreparationTimeMinutes;
            recipe.CookingTimeMinutes = updatedRecipe.CookingTimeMinutes;
            recipe.Servings = updatedRecipe.Servings;
            recipe.Image = updatedRecipe.Image;
            recipe.ImageContentType = updatedRecipe.ImageContentType;
            recipe.CategoryId = updatedRecipe.CategoryId;
            recipe.UpdatedAt = DateTime.UtcNow;

            // Aktualizacja sk³adników - usuñ stare, dodaj nowe
            db.RecipeIngredients.RemoveRange(recipe.Ingredients);
            foreach (var ingredient in updatedRecipe.Ingredients)
            {
                ingredient.RecipeId = recipe.Id;
                db.RecipeIngredients.Add(ingredient);
            }

            await db.SaveChangesAsync().ConfigureAwait(false);
            return Results.Ok(recipe);
        })
        .WithName("UpdateRecipe");

        // DELETE - soft delete
        group.MapDelete("/{id:guid}", async (Guid id, RecipeDbContext db) =>
        {
            var recipe = await db.Recipes.FindAsync(id).ConfigureAwait(false);
            if (recipe is null)
                return Results.NotFound();

            recipe.IsDeleted = true;
            recipe.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync().ConfigureAwait(false);

            return Results.NoContent();
        })
        .WithName("DeleteRecipe");

        // Search
        group.MapGet("/search", async (string query, RecipeDbContext db) =>
        {
            var recipes = await db.Recipes
                .AsNoTracking()
                .Include(r => r.Category)
                .Where(r => !r.IsDeleted &&
                    (r.Title.Contains(query) || r.Description.Contains(query)))
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync().ConfigureAwait(false);
            return Results.Ok(recipes);
        })
        .WithName("SearchRecipes");
    }
}

