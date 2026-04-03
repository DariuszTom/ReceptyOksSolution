using FluentValidation;

namespace ReceptyOks.Api.Validators;

public class MealPlanSyncDtoValidator : AbstractValidator<MealPlanSyncDto>
{
    public MealPlanSyncDtoValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("MealPlan Id cannot be empty");

        RuleFor(x => x.Date)
            .NotEmpty()
            .WithMessage("Date is required");

        RuleFor(x => x.RecipeId)
            .NotEmpty()
            .WithMessage("RecipeId cannot be empty");

        RuleFor(x => x.Notes)
            .MaximumLength(500)
            .When(x => !string.IsNullOrEmpty(x.Notes))
            .WithMessage("Notes cannot exceed 500 characters");
    }
}
