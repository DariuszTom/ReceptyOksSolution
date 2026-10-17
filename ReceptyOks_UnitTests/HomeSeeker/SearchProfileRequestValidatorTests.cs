using HomeSeeker.Models;
using ReceptyOks.Api.Validators;

namespace ReceptyOks_UnitTests.HomeSeeker;

/// <summary>
/// Tests the property-type rule of <see cref="SearchProfileRequestValidator"/>.
/// </summary>
public sealed class SearchProfileRequestValidatorTests
{
    private readonly SearchProfileRequestValidator _validator = new();

    [TestCase(PropertyTypes.House)]
    [TestCase(PropertyTypes.Apartment)]
    [TestCase(PropertyTypes.All)]
    public void Validate_WithKnownPropertyTypes_HasNoPropertyTypeError(PropertyTypes types)
    {
        var result = _validator.Validate(Request(types));

        Assert.That(result.Errors.Where(e => e.PropertyName == nameof(SearchProfileRequest.PropertyTypes)), Is.Empty);
    }

    [TestCase(PropertyTypes.None)]
    [TestCase((PropertyTypes)4)]
    [TestCase((PropertyTypes)7)]
    public void Validate_WithNoneOrUnknownFlags_IsRejected(PropertyTypes types)
    {
        var result = _validator.Validate(Request(types));

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Select(e => e.PropertyName), Does.Contain(nameof(SearchProfileRequest.PropertyTypes)));
    }

    [TestCase(null, null)]
    [TestCase(51.1, 17.03)]
    [TestCase(-90.0, 180.0)]
    public void Validate_WithValidOrNoHome_HasNoHomeError(double? lat, double? lon)
    {
        var request = Request(PropertyTypes.House) with { HomeLatitude = lat, HomeLongitude = lon };

        var result = _validator.Validate(request);

        Assert.That(result.Errors.Where(e => e.PropertyName.StartsWith("Home")), Is.Empty);
    }

    [TestCase(51.1, null)]
    [TestCase(null, 17.03)]
    [TestCase(91.0, 17.03)]
    [TestCase(51.1, -181.0)]
    public void Validate_WithPartialOrOutOfRangeHome_IsRejected(double? lat, double? lon)
    {
        var request = Request(PropertyTypes.House) with { HomeLatitude = lat, HomeLongitude = lon };

        var result = _validator.Validate(request);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Select(e => e.PropertyName), Has.Some.StartsWith("Home"));
    }

    private static SearchProfileRequest Request(PropertyTypes types) => new()
    {
        City = "Wrocław",
        NotificationEmail = "a@example.com",
        PropertyTypes = types
    };
}
