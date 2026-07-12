using System.Text;
using System.Text.Json;

namespace ReceptyOks.Shared.AI;

/// <summary>
/// Parser for extracting conversation history from serialized AI session JSON.
/// Supports various JSON formats including Microsoft.Agents.AI and Anthropic.
/// </summary>
public static class ConversationHistoryParser
{
    /// <summary>
    /// Extracts conversation messages from a serialized session JSON.
    /// Parses the JSON structure to retrieve user and assistant messages.
    /// </summary>
    /// <param name="serializedThread">The JSON string containing the serialized conversation.</param>
    /// <returns>List of conversation messages with role and content.</returns>
    public static IReadOnlyList<ConversationMessage> Parse(string serializedThread)
    {
        if (string.IsNullOrWhiteSpace(serializedThread))
        {
            return [];
        }

        var messages = new List<ConversationMessage>();

        try
        {
            using var doc = JsonDocument.Parse(serializedThread);
            var root = doc.RootElement;

            var messagesElement = FindMessagesArray(root);

            if (messagesElement.HasValue)
            {
                foreach (var message in messagesElement.Value.EnumerateArray())
                {
                    var role = GetStringProperty(message, "Role", "role") ?? "unknown";
                    var content = ExtractMessageContent(message);

                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        var isUser = role.Equals("user", StringComparison.OrdinalIgnoreCase);
                        messages.Add(new ConversationMessage(content, isUser));
                    }
                }
            }
        }
        catch (JsonException)
        {
            // If parsing fails, return empty list
        }

        return messages;
    }

    private static JsonElement? FindMessagesArray(JsonElement element)
    {
        // Direct Messages property
        if (TryGetArrayProperty(element, out var messagesElement, "Messages", "messages"))
        {
            return messagesElement;
        }

        // ChatHistory property (Microsoft.Agents.AI format)
        if (element.TryGetProperty("ChatHistory", out var chatHistoryElement) ||
            element.TryGetProperty("chatHistory", out chatHistoryElement))
        {
            if (chatHistoryElement.ValueKind == JsonValueKind.Array)
            {
                return chatHistoryElement;
            }

            if (TryGetArrayProperty(chatHistoryElement, out messagesElement, "Messages", "messages"))
            {
                return messagesElement;
            }
        }

        // Try nested State property
        if (element.TryGetProperty("State", out var stateElement) ||
            element.TryGetProperty("state", out stateElement))
        {
            return FindMessagesArray(stateElement);
        }

        // Try stateBag property (InMemoryChatHistoryProvider format)
        if (element.TryGetProperty("stateBag", out var stateBagElement) ||
            element.TryGetProperty("StateBag", out stateBagElement))
        {
            return FindMessagesArray(stateBagElement);
        }

        // Try InMemoryChatHistoryProvider format
        if (element.TryGetProperty("InMemoryChatHistoryProvider", out var providerElement) ||
            element.TryGetProperty("inMemoryChatHistoryProvider", out providerElement))
        {
            return FindMessagesArray(providerElement);
        }

        // Recursively search in object properties
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Object)
                {
                    var found = FindMessagesArray(prop.Value);
                    if (found.HasValue)
                    {
                        return found;
                    }
                }
                else if (prop.Value.ValueKind == JsonValueKind.Array)
                {
                    var arrayResult = CheckArrayForMessages(prop.Value);
                    if (arrayResult.HasValue)
                    {
                        return arrayResult;
                    }
                }
            }
        }

        return null;
    }

    private static JsonElement? CheckArrayForMessages(JsonElement array)
    {
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object)
            {
                if (item.TryGetProperty("Role", out _) || item.TryGetProperty("role", out _))
                {
                    return array;
                }

                var found = FindMessagesArray(item);
                if (found.HasValue)
                {
                    return found;
                }
            }
            break; // Only check first item for performance
        }
        return null;
    }

    private static bool TryGetArrayProperty(JsonElement element, out JsonElement result, params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            if (element.TryGetProperty(name, out result) && result.ValueKind == JsonValueKind.Array)
            {
                return true;
            }
        }
        result = default;
        return false;
    }

    private static string? GetStringProperty(JsonElement element, params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            if (element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString();
            }
        }
        return null;
    }

    private static string ExtractMessageContent(JsonElement message)
    {
        // Try to get content directly as string
        if (message.TryGetProperty("Content", out var contentElement) ||
            message.TryGetProperty("content", out contentElement))
        {
            if (contentElement.ValueKind == JsonValueKind.String)
            {
                return contentElement.GetString() ?? string.Empty;
            }

            if (contentElement.ValueKind == JsonValueKind.Array)
            {
                return ExtractTextFromContentArray(contentElement);
            }
        }

        // Try Contents array (Microsoft.Extensions.AI format)
        if (message.TryGetProperty("Contents", out var contentsElement) ||
            message.TryGetProperty("contents", out contentsElement))
        {
            if (contentsElement.ValueKind == JsonValueKind.Array)
            {
                return ExtractTextFromContentArray(contentsElement);
            }
        }

        // Try rawRepresentation for Anthropic format
        if (message.TryGetProperty("rawRepresentation", out var rawElement) ||
            message.TryGetProperty("RawRepresentation", out rawElement))
        {
            if (rawElement.TryGetProperty("content", out var anthropicContent))
            {
                if (anthropicContent.ValueKind == JsonValueKind.Array)
                {
                    return ExtractTextFromContentArray(anthropicContent);
                }
            }
        }

        return string.Empty;
    }

    private static string ExtractTextFromContentArray(JsonElement contentArray)
    {
        var textParts = new StringBuilder();

        foreach (var item in contentArray.EnumerateArray())
        {
            string? text = null;

            if (item.ValueKind == JsonValueKind.String)
            {
                text = item.GetString();
            }
            else if (item.ValueKind == JsonValueKind.Object)
            {
                text = GetStringProperty(item, "Text", "text");

                // Try Anthropic format: { "type": "text", "text": "..." }
                if (string.IsNullOrEmpty(text))
                {
                    var type = GetStringProperty(item, "type", "Type");
                    if (type == "text")
                    {
                        text = GetStringProperty(item, "text", "Text");
                    }
                }

                // Try Value property (some formats)
                if (string.IsNullOrEmpty(text))
                {
                    text = GetStringProperty(item, "Value", "value");
                }
            }

            if (!string.IsNullOrEmpty(text))
            {
                if (textParts.Length > 0) textParts.Append(' ');
                textParts.Append(text);
            }
        }

        return textParts.ToString();
    }
}
