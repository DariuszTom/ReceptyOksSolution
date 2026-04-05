namespace ReceptyOks.Api.Services;

/// <summary>
/// Tracks statistics for sync operations on a specific entity type.
/// </summary>
public class SyncStatistics
{
    public int Added { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public int SkippedInvalidReferences { get; set; }

    public void LogSummary(ILogger logger, string entityType)
    {
        logger.LogInformation(
            "{EntityType} processed - Added: {Added}, Updated: {Updated}, Skipped: {Skipped}, SkippedInvalidReferences: {InvalidRefs}",
            entityType, Added, Updated, Skipped, SkippedInvalidReferences);
    }
}

/// <summary>
/// Container for all entity type statistics during a sync operation.
/// </summary>
public class SyncOperationStatistics
{
    public SyncStatistics Categories { get; } = new();
    public SyncStatistics Ingredients { get; } = new();
    public SyncStatistics Recipes { get; } = new();
    public SyncStatistics MealPlans { get; } = new();

    public void LogAllSummaries(ILogger logger)
    {
        Categories.LogSummary(logger, "Categories");
        Ingredients.LogSummary(logger, "Ingredients");
        Recipes.LogSummary(logger, "Recipes");
        MealPlans.LogSummary(logger, "MealPlans");
    }
}
