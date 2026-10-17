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

        RuleFor(x => x.PropertyTypes)
            .Must(t => t != PropertyTypes.None && (t & ~PropertyTypes.All) == 0)
            .WithMessage("Select at least one property type (house or apartment)");

        RuleFor(x => x.MinPrice)
            .GreaterThanOrEqualTo(0).WithMessage("Minimum price cannot be negative")
            .When(x => x.MinPrice.HasValue);

        RuleFor(x => x.MinPrice)
            .LessThan(x => x.MaxPrice).WithMessage("Minimum price must be less than maximum price")
            .When(x => x.MinPrice.HasValue && x.MaxPrice.HasValue);

        RuleFor(x => x.MaxPrice)
            .GreaterThan(0).WithMessage("Maximum price must be greater than zero")
            .When(x => x.MaxPrice.HasValue);

        RuleFor(x => x.MinAreaSqm)
            .GreaterThanOrEqualTo(0).WithMessage("Minimum area cannot be negative")
            .When(x => x.MinAreaSqm.HasValue);

        RuleFor(x => x.MinAreaSqm)
            .LessThan(x => x.MaxAreaSqm).WithMessage("Minimum area must be less than maximum area")
            .When(x => x.MinAreaSqm.HasValue && x.MaxAreaSqm.HasValue);

        RuleFor(x => x.MaxAreaSqm)
            .GreaterThan(0).WithMessage("Maximum area must be greater than zero")
            .When(x => x.MaxAreaSqm.HasValue);

        RuleFor(x => x.MinRooms)
            .InclusiveBetween(1, 20).WithMessage("Minimum rooms must be between 1 and 20")
            .When(x => x.MinRooms.HasValue);

        RuleFor(x => x.ExtraCriteria)
            .MaximumLength(2000).WithMessage("Extra criteria must be at most 2000 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.ExtraCriteria));

        RuleFor(x => x)
            .Must(x => x.HomeLatitude.HasValue == x.HomeLongitude.HasValue)
            .WithName("HomeLatitude")
            .WithMessage("Home location requires both latitude and longitude");

        RuleFor(x => x.HomeLatitude)
            .InclusiveBetween(-90, 90).WithMessage("Home latitude must be between -90 and 90")
            .When(x => x.HomeLatitude.HasValue);

        RuleFor(x => x.HomeLongitude)
            .InclusiveBetween(-180, 180).WithMessage("Home longitude must be between -180 and 180")
            .When(x => x.HomeLongitude.HasValue);

        RuleFor(x => x.HomeLabel)
            .MaximumLength(200).WithMessage("Home label must be at most 200 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.HomeLabel));

        // Validate the parsed/normalized list rather than the raw string: ",;" must be
        // rejected as empty, and the stored value is the normalized form (", "-joined),
        // which can be longer than the raw input.
        RuleFor(x => x.NotificationEmail)
            .Cascade(CascadeMode.Stop)
            .Must(v => NotificationEmails.Parse(v).Count > 0)
                .WithMessage("At least one notification email is required")
            .Must(v => NotificationEmails.Normalize(v).Length <= NotificationEmails.MaxLength)
                .WithMessage($"Notification email list must be at most {NotificationEmails.MaxLength} characters")
            .Must(v => NotificationEmails.Parse(v).Count <= NotificationEmails.MaxRecipients)
                .WithMessage($"At most {NotificationEmails.MaxRecipients} notification emails are allowed")
            .Must(v => NotificationEmails.Parse(v).All(IsValidEmail))
                .WithMessage("Invalid email address format");
    }

    private static bool IsValidEmail(string address)
    {
        // Same rule FluentValidation's EmailAddress() uses: a single '@' with text on both sides.
        var at = address.IndexOf('@');
        return at > 0 && at == address.LastIndexOf('@') && at < address.Length - 1;
    }
}
