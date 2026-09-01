using FluentValidation;
using HomeSeeker.Models;

namespace ReceptyOks.Api.Validators;

/// <summary>
/// Validator for SearchProfileRequest.
/// </summary>
public sealed class SearchProfileRequestValidator : AbstractValidator<SearchProfileRequest>
{
    public SearchProfileRequestValidator()
    {
        RuleFor(x => x.City)
            .NotEmpty().WithMessage("City is required")
            .MaximumLength(100).WithMessage("City must be at most 100 characters")
            .Matches(@"^[\p{L}\s\-]+$").WithMessage("City can only contain letters, spaces, and hyphens");

        RuleFor(x => x.District)
            .MaximumLength(100).WithMessage("District must be at most 100 characters")
            .Matches(@"^[\p{L}\s\-]*$").WithMessage("District can only contain letters, spaces, and hyphens")
            .When(x => !string.IsNullOrWhiteSpace(x.District));

        RuleFor(x => x.MinPrice)
            .GreaterThanOrEqualTo(0).WithMessage("Minimum price cannot be negative")
            .LessThan(x => x.MaxPrice).WithMessage("Minimum price must be less than maximum price")
            .When(x => x.MinPrice.HasValue && x.MaxPrice.HasValue);

        RuleFor(x => x.MaxPrice)
            .GreaterThan(0).WithMessage("Maximum price must be greater than zero")
            .When(x => x.MaxPrice.HasValue);

        RuleFor(x => x.MinAreaSqm)
            .GreaterThanOrEqualTo(0).WithMessage("Minimum area cannot be negative")
            .LessThan(x => x.MaxAreaSqm).WithMessage("Minimum area must be less than maximum area")
            .When(x => x.MinAreaSqm.HasValue && x.MaxAreaSqm.HasValue);

        RuleFor(x => x.MaxAreaSqm)
            .GreaterThan(0).WithMessage("Maximum area must be greater than zero")
            .When(x => x.MaxAreaSqm.HasValue);

        RuleFor(x => x.ExtraCriteria)
            .MaximumLength(2000).WithMessage("Extra criteria must be at most 2000 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.ExtraCriteria));

        RuleFor(x => x.NotificationEmail)
            .NotEmpty().WithMessage("Notification email is required")
            .MaximumLength(200).WithMessage("Email must be at most 200 characters")
            .EmailAddress().WithMessage("Invalid email address format");
    }
}
