using ReceptyOks.Shared.AI;

namespace ReceptyOks_UnitTests.AI;

/// <summary>
/// Unit tests for the <see cref="ConversationHistoryParser"/> class.
/// </summary>
public sealed class ConversationHistoryParserTests
{
    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Parse_NullOrWhitespace_ReturnsEmptyList(string? input)
    {
        var result = ConversationHistoryParser.Parse(input!);
        Assert.That(result, Is.Empty);
    }

    [TestCase("not json")]
    [TestCase("{invalid}")]
    public void Parse_InvalidJson_ReturnsEmptyList(string input)
    {
        var result = ConversationHistoryParser.Parse(input);
        Assert.That(result, Is.Empty);
    }

    [Test]
    public void Parse_WithMessagesArray_ReturnsMessages()
    {
        var json = """
        {
            "Messages": [
                { "Role": "user", "Content": "Hello" },
                { "Role": "assistant", "Content": "Hi there!" }
            ]
        }
        """;

        var result = ConversationHistoryParser.Parse(json);

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result[0].Content, Is.EqualTo("Hello"));
        Assert.That(result[0].IsUser, Is.True);
        Assert.That(result[1].Content, Is.EqualTo("Hi there!"));
        Assert.That(result[1].IsUser, Is.False);
    }

    [Test]
    public void Parse_WithContentsArray_ReturnsMessages()
    {
        var json = """
        {
            "Messages": [
                { "Role": "user", "Contents": [{ "Text": "Hello world" }] },
                { "Role": "assistant", "Contents": [{ "Text": "Response text" }] }
            ]
        }
        """;

        var result = ConversationHistoryParser.Parse(json);

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result[0].Content, Is.EqualTo("Hello world"));
        Assert.That(result[0].IsUser, Is.True);
        Assert.That(result[1].Content, Is.EqualTo("Response text"));
        Assert.That(result[1].IsUser, Is.False);
    }

    [Test]
    public void Parse_EmptyMessagesArray_ReturnsEmptyList()
    {
        var json = """{ "Messages": [] }""";

        var result = ConversationHistoryParser.Parse(json);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void Parse_MessagesWithEmptyContent_SkipsEmptyMessages()
    {
        var json = """
        {
            "Messages": [
                { "Role": "user", "Content": "Valid message" },
                { "Role": "assistant", "Content": "" },
                { "Role": "user", "Content": "Another valid" }
            ]
        }
        """;

        var result = ConversationHistoryParser.Parse(json);

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result[0].Content, Is.EqualTo("Valid message"));
        Assert.That(result[1].Content, Is.EqualTo("Another valid"));
    }

    [Test]
    public void Parse_WithNestedChatHistory_ReturnsMessages()
    {
        var json = """
        {
            "ChatHistory": [
                { "Role": "user", "Contents": [{ "Text": "First question" }] },
                { "Role": "assistant", "Contents": [{ "Text": "First answer" }] }
            ]
        }
        """;

        var result = ConversationHistoryParser.Parse(json);

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result[0].Content, Is.EqualTo("First question"));
        Assert.That(result[0].IsUser, Is.True);
        Assert.That(result[1].Content, Is.EqualTo("First answer"));
        Assert.That(result[1].IsUser, Is.False);
    }

    [Test]
    public void Parse_WithNestedStateMessages_ReturnsMessages()
    {
        var json = """
        {
            "State": {
                "Messages": [
                    { "Role": "user", "Content": "Nested question" },
                    { "Role": "assistant", "Content": "Nested answer" }
                ]
            }
        }
        """;

        var result = ConversationHistoryParser.Parse(json);

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result[0].Content, Is.EqualTo("Nested question"));
        Assert.That(result[1].Content, Is.EqualTo("Nested answer"));
    }

    [Test]
    public void Parse_WithAnthropicContentFormat_ReturnsMessages()
    {
        var json = """
        {
            "Messages": [
                { "Role": "user", "Contents": [{ "type": "text", "text": "User message" }] },
                { "Role": "assistant", "Contents": [{ "type": "text", "text": "Assistant response" }] }
            ]
        }
        """;

        var result = ConversationHistoryParser.Parse(json);

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result[0].Content, Is.EqualTo("User message"));
        Assert.That(result[0].IsUser, Is.True);
        Assert.That(result[1].Content, Is.EqualTo("Assistant response"));
        Assert.That(result[1].IsUser, Is.False);
    }

    [Test]
    public void Parse_WithStateBagFormat_ReturnsMessages()
    {
        var json = """
        {
            "stateBag": {
                "InMemoryChatHistoryProvider": {
                    "messages": [
                        { "role": "user", "contents": [{ "text": "Hello from stateBag" }] },
                        { "role": "assistant", "contents": [{ "text": "Response from stateBag" }] }
                    ]
                }
            }
        }
        """;

        var result = ConversationHistoryParser.Parse(json);

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result[0].Content, Is.EqualTo("Hello from stateBag"));
        Assert.That(result[0].IsUser, Is.True);
        Assert.That(result[1].Content, Is.EqualTo("Response from stateBag"));
        Assert.That(result[1].IsUser, Is.False);
    }

    [Test]
    public void Parse_WithCamelCaseProperties_ReturnsMessages()
    {
        var json = """
        {
            "messages": [
                { "role": "user", "content": "camelCase message" },
                { "role": "assistant", "content": "camelCase response" }
            ]
        }
        """;

        var result = ConversationHistoryParser.Parse(json);

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result[0].Content, Is.EqualTo("camelCase message"));
        Assert.That(result[1].Content, Is.EqualTo("camelCase response"));
    }

    [Test]
    public void Parse_WithMultipleTextContents_ConcatenatesText()
    {
        var json = """
        {
            "Messages": [
                { "Role": "user", "Contents": [
                    { "Text": "First part" },
                    { "Text": "Second part" }
                ]}
            ]
        }
        """;

        var result = ConversationHistoryParser.Parse(json);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Content, Is.EqualTo("First part Second part"));
    }

    [Test]
    public void Parse_NoMessagesFound_ReturnsEmptyList()
    {
        var json = """{ "SomeOtherField": "value" }""";

        var result = ConversationHistoryParser.Parse(json);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void Parse_MessageWithoutRole_TreatsAsNonUser()
    {
        var json = """
        {
            "Messages": [
                { "Content": "No role message" }
            ]
        }
        """;

        var result = ConversationHistoryParser.Parse(json);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Content, Is.EqualTo("No role message"));
        Assert.That(result[0].IsUser, Is.False);
    }

    [Test]
    public void Parse_WithValuePropertyFallback_ReturnsMessages()
    {
        var json = """
        {
            "Messages": [
                { "Role": "user", "Contents": [{ "Value": "Value based text" }] }
            ]
        }
        """;

        var result = ConversationHistoryParser.Parse(json);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Content, Is.EqualTo("Value based text"));
    }

    [Test]
    public void Parse_WithRawRepresentationAnthropicFormat_ReturnsMessages()
    {
        var json = """
        {
            "Messages": [
                {
                    "Role": "assistant",
                    "rawRepresentation": {
                        "content": [
                            { "type": "text", "text": "Raw representation text" }
                        ]
                    }
                }
            ]
        }
        """;

        var result = ConversationHistoryParser.Parse(json);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Content, Is.EqualTo("Raw representation text"));
        Assert.That(result[0].IsUser, Is.False);
    }

    [Test]
    public void Parse_WithDataUriAttachment_ReturnsMessageWithAttachment()
    {
        var base64 = Convert.ToBase64String([1, 2, 3, 4]);
        var json = $$"""
        {
            "Messages": [
                {
                    "Role": "user",
                    "Contents": [
                        { "$type": "data", "uri": "data:image/png;base64,{{base64}}", "mediaType": "image/png", "name": "pic.png" }
                    ]
                }
            ]
        }
        """;

        var result = ConversationHistoryParser.Parse(json);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Content, Is.Empty);
        Assert.That(result[0].AttachmentBytes, Is.EqualTo(new byte[] { 1, 2, 3, 4 }));
        Assert.That(result[0].AttachmentMediaType, Is.EqualTo("image/png"));
        Assert.That(result[0].AttachmentFileName, Is.EqualTo("pic.png"));
    }

    [Test]
    public void Parse_WithAnthropicImageSourceAttachment_ReturnsMessageWithAttachment()
    {
        var base64 = Convert.ToBase64String([9, 8, 7]);
        var json = $$"""
        {
            "Messages": [
                {
                    "Role": "user",
                    "Contents": [
                        {
                            "type": "image",
                            "source": {
                                "type": "base64",
                                "media_type": "image/jpeg",
                                "data": "{{base64}}"
                            }
                        }
                    ]
                }
            ]
        }
        """;

        var result = ConversationHistoryParser.Parse(json);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].AttachmentBytes, Is.EqualTo(new byte[] { 9, 8, 7 }));
        Assert.That(result[0].AttachmentMediaType, Is.EqualTo("image/jpeg"));
    }

    [Test]
    public void Parse_WithNestedObjectRequiringRecursiveSearch_ReturnsMessages()
    {
        var json = """
        {
            "Wrapper": {
                "Inner": {
                    "Messages": [
                        { "Role": "user", "Content": "Deep nested" }
                    ]
                }
            }
        }
        """;

        var result = ConversationHistoryParser.Parse(json);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Content, Is.EqualTo("Deep nested"));
    }

    [Test]
    public void Parse_WithArrayPropertyContainingRoleObjects_DetectsAsMessagesArray()
    {
        var json = """
        {
            "SomeRandomField": [
                { "role": "user", "content": "Found via array scan" }
            ]
        }
        """;

        var result = ConversationHistoryParser.Parse(json);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Content, Is.EqualTo("Found via array scan"));
    }
}
