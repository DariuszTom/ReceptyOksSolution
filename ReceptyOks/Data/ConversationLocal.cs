using SQLite;

namespace ReceptyOks.Data;

/// <summary>
/// Local database model for storing AI conversation threads.
/// </summary>
[Table("Conversations")]
public class ConversationLocal
{
    [PrimaryKey]
    public string Id { get; set; } = string.Empty;

    public string? Title { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Serialized AgentThread JSON data.
    /// </summary>
    public string SerializedThread { get; set; } = string.Empty;

    public bool IsDeleted { get; set; }
}
