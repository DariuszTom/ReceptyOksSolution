using Microsoft.EntityFrameworkCore.Internal;

namespace ReceptyOks.Api.Middleware
{
    public interface IRecipeDbContext
    {
        DbSet<Category> Categories { get; }
        DbSet<Ingredient> Ingredients { get; }
        DbSet<MealPlan> MealPlans { get; }
        DbSet<RecipeIngredient> RecipeIngredients { get; }
        DbSet<Recipe> Recipes { get; }
        DbSet<ShoppingListItem> ShoppingListItems { get; }
    }
}