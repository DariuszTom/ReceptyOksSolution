using FluentValidation;
using ReceptyOks.Shared.Interfaces;

namespace ReceptyOks.Api.Validators;

/// <summary>
/// Walidator dla CategorySyncDto - używa interfejsu ICategoryData
/// </summary>
public class CategorySyncDtoValidator : AbstractValidator<CategorySyncDto>
{
    public CategorySyncDtoValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Category Id cannot be empty");

        Include(new CategoryDataValidator());
    }
}

/// <summary>
/// Wspólny walidator dla wszystkich klas implementujących ICategoryData
/// Może być używany dla Category, CategorySyncDto i innych
/// </summary>
public class CategoryDataValidator : AbstractValidator<ICategoryData>
{
    public CategoryDataValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Category Name is required")
            .MaximumLength(100)
            .WithMessage("Category Name cannot exceed 100 characters");

        RuleFor(x => x.Description)
            .MaximumLength(500)
            .When(x => !string.IsNullOrEmpty(x.Description))
            .WithMessage("Category Description cannot exceed 500 characters");

        RuleFor(x => x.IconName)
            .MaximumLength(50)
            .When(x => !string.IsNullOrEmpty(x.IconName))
            .WithMessage("Category IconName cannot exceed 50 characters");
    }
}
