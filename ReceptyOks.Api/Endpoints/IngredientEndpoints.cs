namespace ReceptyOks.Api.Endpoints;

public static class IngredientEndpoints
{
    public static void MapIngredientEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/ingredients")
            .WithTags("Ingredients")
            .RequireRateLimiting("fixed");

        // GET - all ingredients
        group.MapGet("/", async (RecipeDbContext db) =>
        {
            var ingredients = await db.Ingredients
                .Where(i => !i.IsDeleted)
                .OrderBy(i => i.Name)
                .ToListAsync().ConfigureAwait(false);
            return Results.Ok(ingredients);
        })
        .WithName("GetAllIngredients");

        // GET - single ingredient
        group.MapGet("/{id:guid}", async (Guid id, RecipeDbContext db) =>
        {
            var ingredient = await db.Ingredients.FindAsync(id).ConfigureAwait(false);
            return ingredient is null ? Results.NotFound() : Results.Ok(ingredient);
        })
        .WithName("GetIngredientById");

        // POST - create new ingredient
        group.MapPost("/", async (Ingredient ingredient, RecipeDbContext db) =>
        {
            ingredient.Id = ingredient.Id == Guid.Empty ? Guid.NewGuid() : ingredient.Id;
            ingredient.CreatedAt = DateTime.UtcNow;
            ingredient.UpdatedAt = DateTime.UtcNow;

            db.Ingredients.Add(ingredient);
            await db.SaveChangesAsync().ConfigureAwait(false);

            return Results.Created($"/api/ingredients/{ingredient.Id}", ingredient);
        })
        .WithName("CreateIngredient");

        // PUT - update ingredient
        group.MapPut("/{id:guid}", async (Guid id, Ingredient updatedIngredient, RecipeDbContext db) =>
        {
            var ingredient = await db.Ingredients.FindAsync(id).ConfigureAwait(false);
            if (ingredient is null)
                return Results.NotFound();

            ingredient.Name = updatedIngredient.Name;
            ingredient.Unit = updatedIngredient.Unit;
            ingredient.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync().ConfigureAwait(false);
            return Results.Ok(ingredient);
        })
        .WithName("UpdateIngredient");

        // DELETE - soft delete
        group.MapDelete("/{id:guid}", async (Guid id, RecipeDbContext db) =>
        {
            var ingredient = await db.Ingredients.FindAsync(id).ConfigureAwait(false);
            if (ingredient is null)
                return Results.NotFound();

            ingredient.IsDeleted = true;
            ingredient.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync().ConfigureAwait(false);

            return Results.NoContent();
        })
        .WithName("DeleteIngredient");

        // Search
        group.MapGet("/search", async (string query, RecipeDbContext db) =>
        {
            var ingredients = await db.Ingredients
                .AsNoTracking()
                .Where(i => !i.IsDeleted && i.Name.Contains(query))
                .OrderBy(i => i.Name)
                .Take(20)
                .ToListAsync().ConfigureAwait(false);
            return Results.Ok(ingredients);
        })
        .WithName("SearchIngredients");
    }
}

