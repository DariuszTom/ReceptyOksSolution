using Microsoft.EntityFrameworkCore;
using ReceptyOks.Api.Middleware;
using ReceptyOks.Shared.DTOs;
using ReceptyOks.Shared.Models;

namespace ReceptyOks.Api.Endpoints;

/// <summary>
/// Endpoints for managing the central shopping list.
/// The list is stored only on the backend to avoid discrepancies
/// when multiple people use the same list.
/// </summary>
public static class ShoppingListEndpoints
{
    public static void MapShoppingListEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/shopping-list")
            .WithTags("ShoppingList")
            .RequireRateLimiting("fixed");

        // GET - entire shopping list (active items)
        group.MapGet("/", async (RecipeDbContext db, bool? includeBought) =>
        {
            var query = db.ShoppingListItems
                .AsNoTracking()
                .Where(s => !s.IsDeleted);

            if (includeBought != true)
            {
                query = query.Where(s => !s.IsBought);
            }

            var items = await query
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

            return Results.Ok(items);
        })
        .Produces<List<ShoppingListItem>>()
        .WithName("GetShoppingList");

        // GET - single item
        group.MapGet("/{id:guid}", async (Guid id, RecipeDbContext db) =>
        {
            var item = await db.ShoppingListItems
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);

            return item is null ? Results.NotFound() : Results.Ok(item);
        })
        .Produces<ShoppingListItem>()
        .Produces(StatusCodes.Status404NotFound)
        .WithName("GetShoppingListItem");

        // POST - add new item
        group.MapPost("/", async (ShoppingListItem item, RecipeDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(item.Name))
            {
                return Results.BadRequest(new { Error = "Name is required" });
            }

            item.Id = item.Id == Guid.Empty ? Guid.NewGuid() : item.Id;
            item.CreatedAt = DateTime.UtcNow;
            item.UpdatedAt = DateTime.UtcNow;
            item.IsBought = false;
            item.IsDeleted = false;

            db.ShoppingListItems.Add(item);
            await db.SaveChangesAsync();

            return Results.Created($"/api/shopping-list/{item.Id}", item);
        })
        .Produces<ShoppingListItem>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .WithName("AddShoppingListItem");

        // POST - add multiple items at once (e.g., from a recipe)
        group.MapPost("/bulk", async (List<ShoppingListItem> items, RecipeDbContext db) =>
        {
            if (items is null || items.Count == 0)
            {
                return Results.BadRequest(new { Error = "Items list cannot be empty" });
            }

            var invalidItems = items.Where(i => string.IsNullOrWhiteSpace(i.Name)).ToList();
            if (invalidItems.Count > 0)
            {
                return Results.BadRequest(new { Error = "All items must have a name" });
            }

            var now = DateTime.UtcNow;
            foreach (var item in items)
            {
                item.Id = item.Id == Guid.Empty ? Guid.NewGuid() : item.Id;
                item.CreatedAt = now;
                item.UpdatedAt = now;
                item.IsBought = false;
                item.IsDeleted = false;
            }

            db.ShoppingListItems.AddRange(items);
            await db.SaveChangesAsync();

            return Results.Created("/api/shopping-list", items);
        })
        .Produces<List<ShoppingListItem>>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .WithName("AddShoppingListItemsBulk");

        // PUT - update item
        group.MapPut("/{id:guid}", async (Guid id, ShoppingListItem updatedItem, RecipeDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(updatedItem.Name))
            {
                return Results.BadRequest(new { Error = "Name is required" });
            }

            var item = await db.ShoppingListItems.FindAsync(id);
            if (item is null || item.IsDeleted)
            {
                return Results.NotFound();
            }

            item.Name = updatedItem.Name;
            item.Quantity = updatedItem.Quantity;
            item.Unit = updatedItem.Unit;
            item.Note = updatedItem.Note;
            item.IngredientId = updatedItem.IngredientId;
            item.RecipeId = updatedItem.RecipeId;
            item.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();
            return Results.Ok(item);
        })
        .Produces<ShoppingListItem>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .WithName("UpdateShoppingListItem");

        // PATCH - mark as bought
        group.MapPatch("/{id:guid}/bought", async (Guid id, BoughtRequest request, RecipeDbContext db) =>
        {
            var item = await db.ShoppingListItems.FindAsync(id);
            if (item is null || item.IsDeleted)
            {
                return Results.NotFound();
            }

            item.IsBought = true;
            item.BoughtBy = request.BoughtBy;
            item.BoughtAt = DateTime.UtcNow;
            item.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();
            return Results.Ok(item);
        })
        .Produces<ShoppingListItem>()
        .Produces(StatusCodes.Status404NotFound)
        .WithName("MarkAsBought");

        // PATCH - unmark as bought
        group.MapPatch("/{id:guid}/unbought", async (Guid id, RecipeDbContext db) =>
        {
            var item = await db.ShoppingListItems.FindAsync(id);
            if (item is null || item.IsDeleted)
            {
                return Results.NotFound();
            }

            item.IsBought = false;
            item.BoughtBy = null;
            item.BoughtAt = null;
            item.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();
            return Results.Ok(item);
        })
        .Produces<ShoppingListItem>()
        .Produces(StatusCodes.Status404NotFound)
        .WithName("MarkAsUnbought");

        // PATCH - mark multiple as bought (uses ExecuteUpdateAsync for better performance)
        group.MapPatch("/bulk/bought", async (BulkBoughtRequest request, RecipeDbContext db) =>
        {
            if (request.Ids is null || request.Ids.Count == 0)
            {
                return Results.BadRequest(new { Error = "Ids list cannot be empty" });
            }

            var now = DateTime.UtcNow;
            var updatedCount = await db.ShoppingListItems
                .Where(s => request.Ids.Contains(s.Id) && !s.IsDeleted)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(s => s.IsBought, true)
                    .SetProperty(s => s.BoughtBy, request.BoughtBy)
                    .SetProperty(s => s.BoughtAt, now)
                    .SetProperty(s => s.UpdatedAt, now));

            return Results.Ok(new BulkOperationResponse(updatedCount));
        })
        .Produces<BulkOperationResponse>()
        .Produces(StatusCodes.Status400BadRequest)
        .WithName("MarkMultipleAsBought");

        // DELETE - remove item (soft delete)
        group.MapDelete("/{id:guid}", async (Guid id, RecipeDbContext db) =>
        {
            var item = await db.ShoppingListItems.FindAsync(id);
            if (item is null || item.IsDeleted)
            {
                return Results.NotFound();
            }

            item.IsDeleted = true;
            item.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return Results.NoContent();
        })
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .WithName("DeleteShoppingListItem");

        // DELETE - permanently remove item from database (hard delete)
        group.MapDelete("/{id:guid}/permanent", async (Guid id, RecipeDbContext db) =>
        {
            var item = await db.ShoppingListItems.FindAsync(id);
            if (item is null)
            {
                return Results.NotFound();
            }

            db.ShoppingListItems.Remove(item);
            await db.SaveChangesAsync();

            return Results.NoContent();
        })
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .WithName("HardDeleteShoppingListItem");

        // DELETE - clear bought items (uses ExecuteUpdateAsync for better performance)
        group.MapDelete("/clear-bought", async (RecipeDbContext db) =>
        {
            var now = DateTime.UtcNow;
            var deletedCount = await db.ShoppingListItems
                .Where(s => s.IsBought && !s.IsDeleted)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(s => s.IsDeleted, true)
                    .SetProperty(s => s.UpdatedAt, now));

            return Results.Ok(new BulkOperationResponse(deletedCount));
        })
        .Produces<BulkOperationResponse>()
        .WithName("ClearBoughtItems");

        // DELETE - clear entire list (uses ExecuteUpdateAsync for better performance)
        group.MapDelete("/clear-all", async (RecipeDbContext db) =>
        {
            var now = DateTime.UtcNow;
            var deletedCount = await db.ShoppingListItems
                .Where(s => !s.IsDeleted)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(s => s.IsDeleted, true)
                    .SetProperty(s => s.UpdatedAt, now));

            return Results.Ok(new BulkOperationResponse(deletedCount));
        })
        .Produces<BulkOperationResponse>()
        .WithName("ClearAllShoppingList");

        // GET - list statistics
        group.MapGet("/stats", async (RecipeDbContext db) =>
        {
            var stats = await db.ShoppingListItems
                .AsNoTracking()
                .Where(s => !s.IsDeleted)
                .GroupBy(_ => 1)
                .Select(g => new ShoppingListStats(
                    g.Count(),
                    g.Count(s => s.IsBought),
                    g.Count(s => !s.IsBought)))
                .FirstOrDefaultAsync();

            return Results.Ok(stats ?? new ShoppingListStats(0, 0, 0));
        })
        .Produces<ShoppingListStats>()
        .WithName("GetShoppingListStats");
    }
}


