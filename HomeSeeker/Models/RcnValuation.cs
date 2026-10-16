namespace HomeSeeker.Models;

/// <summary>
/// Transaction-price valuation of an apartment based on notarial deeds (RCN register)
/// near its location.
/// </summary>
/// <param name="EstimatedPricePerSqm">Model estimate of the price per m² for this location and area.</param>
/// <param name="MedianPricePerSqm">Median transaction price per m² in the radius.</param>
/// <param name="SampleSize">Number of transactions behind the median.</param>
/// <param name="RadiusKm">Search radius used by the service.</param>
/// <param name="PeriodMonths">How many months of transactions were considered.</param>
public sealed record RcnValuation(
    decimal EstimatedPricePerSqm,
    decimal? MedianPricePerSqm,
    int SampleSize,
    double RadiusKm,
    int PeriodMonths);
