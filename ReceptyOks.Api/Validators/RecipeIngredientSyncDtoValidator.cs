using FluentValidation;

namespace ReceptyOks.Api.Validators;

public class RecipeIngredientSyncDtoValidator : AbstractValidator<RecipeIngredientSyncDto>
{
    public RecipeIngredientSyncDtoValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("RecipeIngredient Id cannot be empty");

        RuleFor(x => x.IngredientId)
            .NotEmpty()
            .WithMessage("IngredientId cannot be empty");

        RuleFor(x => x.Quantity)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Quantity must be non-negative");

        RuleFor(x => x.Unit)
            .MaximumLength(50)
            .When(x => !string.IsNullOrEmpty(x.Unit))
            .WithMessage("Unit cannot exceed 50 characters");

        RuleFor(x => x.Notes)
            .MaximumLength(500)
            .When(x => !string.IsNullOrEmpty(x.Notes))
            .WithMessage("Notes cannot exceed 500 characters");

        RuleFor(x => x.Order)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Order must be non-negative");
    }
}
