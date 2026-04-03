using FluentValidation;

namespace ReceptyOks.Api.Validators;

public class IngredientSyncDtoValidator : AbstractValidator<IngredientSyncDto>
{
    public IngredientSyncDtoValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Ingredient Id cannot be empty");

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Ingredient Name is required")
            .MaximumLength(100)
            .WithMessage("Ingredient Name cannot exceed 100 characters");

        RuleFor(x => x.Unit)
            .MaximumLength(50)
            .When(x => !string.IsNullOrEmpty(x.Unit))
            .WithMessage("Ingredient Unit cannot exceed 50 characters");
    }
}
