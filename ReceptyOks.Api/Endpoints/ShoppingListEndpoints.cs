using Microsoft.EntityFrameworkCore;
using ReceptyOks.Api.Middleware;
using ReceptyOks.Shared.Models;

namespace ReceptyOks.Api.Endpoints;

/// <summary>
/// Endpointy do zarządzania centralną listą zakupów.
/// Lista jest przechowywana tylko na backendzie, aby uniknąć rozbieżności
/// gdy kilka osób korzysta z tej samej listy.
/// </summary>
public static class ShoppingListEndpoints
{
    public static void MapShoppingListEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/shopping-list")
            .WithTags("ShoppingList")
       .RequireRateLimiting("fixed");

        // GET - cała lista zakupów (aktywne elementy)
        group.MapGet("/", async (RecipeDbContext db, bool? includeBought) =>
         {
             var query = db.ShoppingListItems
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
        .WithName("GetShoppingList");

        // GET - pojedynczy element
        group.MapGet("/{id:guid}", async (Guid id, RecipeDbContext db) =>
              {
                  var item = await db.ShoppingListItems
             .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);
                  return item is null ? Results.NotFound() : Results.Ok(item);
              })
              .WithName("GetShoppingListItem");

        // POST - dodaj nowy element
        group.MapPost("/", async (ShoppingListItem item, RecipeDbContext db) =>
   {
       item.Id = item.Id == Guid.Empty ? Guid.NewGuid() : item.Id;
       item.CreatedAt = DateTime.UtcNow;
       item.UpdatedAt = DateTime.UtcNow;
       item.IsBought = false;

       db.ShoppingListItems.Add(item);
       await db.SaveChangesAsync();

       return Results.Created($"/api/shopping-list/{item.Id}", item);
   })
        .WithName("AddShoppingListItem");

        // POST - dodaj wiele elementów naraz (np. z przepisu)
        group.MapPost("/bulk", async (List<ShoppingListItem> items, RecipeDbContext db) =>
             {
                 var now = DateTime.UtcNow;
                 foreach (var item in items)
                 {
                     item.Id = item.Id == Guid.Empty ? Guid.NewGuid() : item.Id;
                     item.CreatedAt = now;
                     item.UpdatedAt = now;
                     item.IsBought = false;
                 }

                 db.ShoppingListItems.AddRange(items);
                 await db.SaveChangesAsync();

                 return Results.Created("/api/shopping-list", items);
             })
             .WithName("AddShoppingListItemsBulk");

        // PUT - aktualizuj element
        group.MapPut("/{id:guid}", async (Guid id, ShoppingListItem updatedItem, RecipeDbContext db) =>
        {
            var item = await db.ShoppingListItems.FindAsync(id);
            if (item is null || item.IsDeleted)
                return Results.NotFound();

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
        .WithName("UpdateShoppingListItem");

        // PATCH - oznacz jako kupione
        group.MapPatch("/{id:guid}/bought", async (Guid id, BoughtRequest request, RecipeDbContext db) =>
          {
              var item = await db.ShoppingListItems.FindAsync(id);
              if (item is null || item.IsDeleted)
                  return Results.NotFound();

              item.IsBought = true;
              item.BoughtBy = request.BoughtBy;
              item.BoughtAt = DateTime.UtcNow;
              item.UpdatedAt = DateTime.UtcNow;

              await db.SaveChangesAsync();
              return Results.Ok(item);
          })
    .WithName("MarkAsBought");

        // PATCH - cofnij oznaczenie jako kupione
        group.MapPatch("/{id:guid}/unbought", async (Guid id, RecipeDbContext db) =>
        {
            var item = await db.ShoppingListItems.FindAsync(id);
            if (item is null || item.IsDeleted)
                return Results.NotFound();

            item.IsBought = false;
            item.BoughtBy = null;
            item.BoughtAt = null;
            item.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();
            return Results.Ok(item);
        })
        .WithName("MarkAsUnbought");

        // PATCH - oznacz wiele jako kupione
        group.MapPatch("/bulk/bought", async (BulkBoughtRequest request, RecipeDbContext db) =>
     {
         var items = await db.ShoppingListItems
         .Where(s => request.Ids.Contains(s.Id) && !s.IsDeleted)
            .ToListAsync();

         var now = DateTime.UtcNow;
         foreach (var item in items)
         {
             item.IsBought = true;
             item.BoughtBy = request.BoughtBy;
             item.BoughtAt = now;
             item.UpdatedAt = now;
         }

         await db.SaveChangesAsync();
         return Results.Ok(items);
     })
            .WithName("MarkMultipleAsBought");

        // DELETE - usuń element (soft delete)
        group.MapDelete("/{id:guid}", async (Guid id, RecipeDbContext db) =>
              {
                  var item = await db.ShoppingListItems.FindAsync(id);
                  if (item is null)
                      return Results.NotFound();

                  item.IsDeleted = true;
                  item.UpdatedAt = DateTime.UtcNow;
                  await db.SaveChangesAsync();

                  return Results.NoContent();
              })
              .WithName("DeleteShoppingListItem");

        // DELETE - wyczyść kupione elementy
        group.MapDelete("/clear-bought", async (RecipeDbContext db) =>
           {
               var boughtItems = await db.ShoppingListItems
               .Where(s => s.IsBought && !s.IsDeleted)
        .ToListAsync();

               var now = DateTime.UtcNow;
               foreach (var item in boughtItems)
               {
                   item.IsDeleted = true;
                   item.UpdatedAt = now;
               }

               await db.SaveChangesAsync();
               return Results.Ok(new { DeletedCount = boughtItems.Count });
           })
    .WithName("ClearBoughtItems");

        // DELETE - wyczyść całą listę
        group.MapDelete("/clear-all", async (RecipeDbContext db) =>
        {
            var allItems = await db.ShoppingListItems
         .Where(s => !s.IsDeleted)
          .ToListAsync();

            var now = DateTime.UtcNow;
            foreach (var item in allItems)
            {
                item.IsDeleted = true;
                item.UpdatedAt = now;
            }

            await db.SaveChangesAsync();
            return Results.Ok(new { DeletedCount = allItems.Count });
        })
  .WithName("ClearAllShoppingList");

        // GET - statystyki listy
        group.MapGet("/stats", async (RecipeDbContext db) =>
        {
            var stats = await db.ShoppingListItems
        .Where(s => !s.IsDeleted)
           .GroupBy(_ => 1)
          .Select(g => new
          {
              TotalItems = g.Count(),
              BoughtItems = g.Count(s => s.IsBought),
              PendingItems = g.Count(s => !s.IsBought)
          })
          .FirstOrDefaultAsync();

            return Results.Ok(stats ?? new { TotalItems = 0, BoughtItems = 0, PendingItems = 0 });
        })
     .WithName("GetShoppingListStats");
    }
}

/// <summary>
/// Request do oznaczania elementu jako kupionego.
/// </summary>
public record BoughtRequest(string? BoughtBy);

/// <summary>
/// Request do oznaczania wielu elementów jako kupionych.
/// </summary>
public record BulkBoughtRequest(List<Guid> Ids, string? BoughtBy);
