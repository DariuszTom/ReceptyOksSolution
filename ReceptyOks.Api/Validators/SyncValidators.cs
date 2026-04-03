using FluentValidation;

namespace ReceptyOks.Api.Validators;

public class SyncRequestValidator : AbstractValidator<SyncRequest>
{
    public SyncRequestValidator()
    {
        RuleFor(x => x.ChangedCategories)
            .NotNull()
            .WithMessage("ChangedCategories cannot be null");

        RuleForEach(x => x.ChangedCategories)
            .SetValidator(new CategorySyncDtoValidator());

        RuleFor(x => x.ChangedIngredients)
            .NotNull()
            .WithMessage("ChangedIngredients cannot be null");

        RuleForEach(x => x.ChangedIngredients)
            .SetValidator(new IngredientSyncDtoValidator());

        RuleFor(x => x.ChangedRecipes)
            .NotNull()
            .WithMessage("ChangedRecipes cannot be null");

        RuleForEach(x => x.ChangedRecipes)
            .SetValidator(new RecipeSyncDtoValidator());

        RuleFor(x => x.ChangedMealPlans)
            .NotNull()
            .WithMessage("ChangedMealPlans cannot be null");

        RuleForEach(x => x.ChangedMealPlans)
            .SetValidator(new MealPlanSyncDtoValidator());
    }
}
