using FluentValidation;

namespace ReceptyOks.Api.Validators;

public class RecipeSyncDtoValidator : AbstractValidator<RecipeSyncDto>
{
    public RecipeSyncDtoValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Recipe Id cannot be empty");

        RuleFor(x => x.Title)
            .NotEmpty()
            .WithMessage("Recipe Title is required")
            .MaximumLength(200)
            .WithMessage("Recipe Title cannot exceed 200 characters");

        // Description and Instructions - NO LIMITS (can be long HTML)

        RuleFor(x => x.PreparationTimeMinutes)
            .GreaterThanOrEqualTo(0)
            .WithMessage("PreparationTimeMinutes must be non-negative");

        RuleFor(x => x.CookingTimeMinutes)
            .GreaterThanOrEqualTo(0)
            .WithMessage("CookingTimeMinutes must be non-negative");

        RuleFor(x => x.Servings)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Servings must be non-negative");

        RuleFor(x => x.Image)
            .Must(img => img == null || img.Length <= 10_000_000)
            .WithMessage("Image size cannot exceed 10MB");

        RuleFor(x => x.ImageContentType)
            .Must(ct => string.IsNullOrEmpty(ct) || ct.StartsWith("image/"))
            .When(x => x.Image != null)
            .WithMessage("ImageContentType must be a valid image MIME type");

        // Ingredients collection can be empty (recipe without ingredients is valid)
        RuleFor(x => x.Ingredients)
            .NotNull()
            .WithMessage("Ingredients cannot be null");

        RuleForEach(x => x.Ingredients)
            .SetValidator(new RecipeIngredientSyncDtoValidator());
    }
}
