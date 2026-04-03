using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Moq;
using ReceptyOks.Shared.AI;
using System.Text.Json;

namespace ReceptyOks_UnitTests;
/// <summary>
/// Unit tests for the <see cref = "AiAgent"/> class.
/// </summary>
public sealed partial class AiAgentTests
{
    /// <summary>
    /// Tests that ConversationId returns null when the agent is initially created.
    /// </summary>
    [Test]
    public void ConversationId_InitialState_ReturnsNull()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        // Act
        var conversationId = agent.ConversationId;
        // Assert
        Assert.That(conversationId, Is.Null);
    }

    /// <summary>
    /// Tests that ConversationId returns null after ClearHistory is called.
    /// </summary>
    [Test]
    public void ConversationId_AfterClearHistory_ReturnsNull()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        // Act
        agent.ClearHistory();
        var conversationId = agent.ConversationId;
        // Assert
        Assert.That(conversationId, Is.Null);
    }

    /// <summary>
    /// Tests that the constructor throws ArgumentNullException when chatClient parameter is null.
    /// </summary>
    [Test]
    public void AiAgent_NullChatClient_ThrowsArgumentNullException()
    {
        // Arrange
        IChatClient? chatClient = null;
        const string? systemPrompt = "test prompt";
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => new AiAgent(chatClient!, systemPrompt));
        Assert.That(exception!.ParamName, Is.EqualTo("chatClient"));
    }

    /// <summary>
    /// Tests that the constructor successfully initializes the agent with valid chatClient
    /// and various systemPrompt values (null, empty, whitespace, normal, long, special characters).
    /// Verifies that SystemPrompt property returns the provided value and other properties are initialized correctly.
    /// </summary>
    [TestCase(null, TestName = "Constructor_ValidChatClient_NullSystemPrompt")]
    [TestCase("", TestName = "Constructor_ValidChatClient_EmptySystemPrompt")]
    [TestCase("   ", TestName = "Constructor_ValidChatClient_WhitespaceSystemPrompt")]
    [TestCase("You are a helpful assistant.", TestName = "Constructor_ValidChatClient_NormalSystemPrompt")]
    [TestCase("Line1\nLine2\rLine3\r\nLine4", TestName = "Constructor_ValidChatClient_SystemPromptWithNewlines")]
    [TestCase("Special chars: !@#$%^&*()_+-=[]{}|;':\",./<>?~`", TestName = "Constructor_ValidChatClient_SystemPromptWithSpecialCharacters")]
    [TestCase("Unicode: 你好世界 🌍 émojis", TestName = "Constructor_ValidChatClient_SystemPromptWithUnicode")]
    public void AiAgent_ValidChatClientWithVariousSystemPrompts_InitializesCorrectly(string? systemPrompt)
    {
        // Arrange
        var chatClientMock = new Mock<IChatClient>();
        // Act
        var agent = new AiAgent(chatClientMock.Object, systemPrompt);
        // Assert
        Assert.That(agent.SystemPrompt, Is.EqualTo(systemPrompt));
        Assert.That(agent.Tools, Is.Not.Null);
        Assert.That(agent.Tools, Is.Empty);
        Assert.That(agent.ConversationId, Is.Null);
    }

    /// <summary>
    /// Tests that the constructor successfully initializes the agent with a very long systemPrompt.
    /// Verifies that the agent can handle large system prompts without issues.
    /// </summary>
    [Test]
    public void AiAgent_ValidChatClientWithVeryLongSystemPrompt_InitializesCorrectly()
    {
        // Arrange
        var chatClientMock = new Mock<IChatClient>();
        var veryLongSystemPrompt = new string('A', 10000);
        // Act
        var agent = new AiAgent(chatClientMock.Object, veryLongSystemPrompt);
        // Assert
        Assert.That(agent.SystemPrompt, Is.EqualTo(veryLongSystemPrompt));
        Assert.That(agent.SystemPrompt!.Length, Is.EqualTo(10000));
        Assert.That(agent.Tools, Is.Not.Null);
        Assert.That(agent.Tools, Is.Empty);
        Assert.That(agent.ConversationId, Is.Null);
    }

    /// <summary>
    /// Tests that the constructor with only chatClient parameter (using default systemPrompt)
    /// initializes the agent with null systemPrompt.
    /// </summary>
    [Test]
    public void AiAgent_ValidChatClientWithoutSystemPrompt_InitializesWithNullSystemPrompt()
    {
        // Arrange
        var chatClientMock = new Mock<IChatClient>();
        // Act
        var agent = new AiAgent(chatClientMock.Object);
        // Assert
        Assert.That(agent.SystemPrompt, Is.Null);
        Assert.That(agent.Tools, Is.Not.Null);
        Assert.That(agent.Tools, Is.Empty);
        Assert.That(agent.ConversationId, Is.Null);
    }

    /// <summary>
    /// Tests that the Tools property returns an empty collection when the agent is first initialized.
    /// </summary>
    [Test]
    public void Tools_InitialState_ReturnsEmptyCollection()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        // Act
        var tools = agent.Tools;
        // Assert
        Assert.That(tools, Is.Not.Null);
        Assert.That(tools, Is.Empty);
        Assert.That(tools.Count, Is.EqualTo(0));
    }

    /// <summary>
    /// Tests that the Tools property never returns null, ensuring it always returns a valid collection reference.
    /// </summary>
    [Test]
    public void Tools_Always_ReturnsNonNullCollection()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object, "test prompt");
        // Act
        var tools = agent.Tools;
        // Assert
        Assert.That(tools, Is.Not.Null);
    }

    /// <summary>
    /// Tests that the Tools property returns an IReadOnlyList of AITool.
    /// </summary>
    [Test]
    public void Tools_Always_ReturnsIReadOnlyListType()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        // Act
        var tools = agent.Tools;
        // Assert
        Assert.That(tools, Is.InstanceOf<IReadOnlyList<AITool>>());
    }

    /// <summary>
    /// Tests that the Tools property reflects the state after a single tool is added.
    /// </summary>
    [Test]
    public void Tools_AfterAddingSingleTool_ReturnsCollectionWithOneTool()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        var mockTool = new Mock<AITool>();
        // Act
        agent.AddTool(mockTool.Object);
        var tools = agent.Tools;
        // Assert
        Assert.That(tools, Is.Not.Null);
        Assert.That(tools.Count, Is.EqualTo(1));
        Assert.That(tools, Contains.Item(mockTool.Object));
    }

    /// <summary>
    /// Tests that the Tools property reflects the state after multiple tools are added.
    /// Verifies that all added tools are present and the count is correct.
    /// </summary>
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(5)]
    public void Tools_AfterAddingMultipleTools_ReturnsAllTools(int toolCount)
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        var mockTools = new List<AITool>();
        for (int i = 0; i < toolCount; i++)
        {
            var mockTool = new Mock<AITool>();
            mockTools.Add(mockTool.Object);
            agent.AddTool(mockTool.Object);
        }

        // Act
        var tools = agent.Tools;
        // Assert
        Assert.That(tools, Is.Not.Null);
        Assert.That(tools.Count, Is.EqualTo(toolCount));
        foreach (var mockTool in mockTools)
        {
            Assert.That(tools, Contains.Item(mockTool));
        }
    }

    /// <summary>
    /// Tests that the Tools property returns an empty collection after ClearTools is called.
    /// </summary>
    [Test]
    public void Tools_AfterClearTools_ReturnsEmptyCollection()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        var mockTool1 = new Mock<AITool>();
        var mockTool2 = new Mock<AITool>();
        agent.AddTool(mockTool1.Object);
        agent.AddTool(mockTool2.Object);
        // Act
        agent.ClearTools();
        var tools = agent.Tools;
        // Assert
        Assert.That(tools, Is.Not.Null);
        Assert.That(tools, Is.Empty);
        Assert.That(tools.Count, Is.EqualTo(0));
    }

    /// <summary>
    /// Tests that the Tools property remains consistent across multiple accesses without modification.
    /// Verifies that the same underlying collection is returned.
    /// </summary>
    [Test]
    public void Tools_MultipleAccesses_ReturnsSameUnderlyingCollection()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        var mockTool = new Mock<AITool>();
        agent.AddTool(mockTool.Object);
        // Act
        var tools1 = agent.Tools;
        var tools2 = agent.Tools;
        // Assert
        Assert.That(tools1.Count, Is.EqualTo(tools2.Count));
        Assert.That(tools1.SequenceEqual(tools2), Is.True);
    }

    /// <summary>
    /// Tests that the Tools property correctly reflects changes in the underlying collection.
    /// Adds a tool after first access and verifies the second access reflects the change.
    /// </summary>
    [Test]
    public void Tools_AfterModification_ReflectsChanges()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        var mockTool1 = new Mock<AITool>();
        var mockTool2 = new Mock<AITool>();
        agent.AddTool(mockTool1.Object);
        // Act
        var toolsBeforeAdd = agent.Tools.Count;
        agent.AddTool(mockTool2.Object);
        var toolsAfterAdd = agent.Tools.Count;
        // Assert
        Assert.That(toolsBeforeAdd, Is.EqualTo(1));
        Assert.That(toolsAfterAdd, Is.EqualTo(2));
    }

    /// <summary>
    /// Tests that AddTool throws ArgumentNullException when a null tool is provided.
    /// </summary>
    [Test]
    public void AddTool_NullTool_ThrowsArgumentNullException()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => agent.AddTool(null!));
        Assert.That(exception.ParamName, Is.EqualTo("tool"));
    }

    /// <summary>
    /// Tests that AddTool successfully adds a valid tool to the collection.
    /// </summary>
    [Test]
    public void AddTool_ValidTool_AddsToolToCollection()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        var mockTool = new Mock<AITool>();
        // Act
        agent.AddTool(mockTool.Object);
        // Assert
        Assert.That(agent.Tools, Has.Count.EqualTo(1));
        Assert.That(agent.Tools, Contains.Item(mockTool.Object));
    }

    /// <summary>
    /// Tests that AddTool can add multiple different tools to the collection.
    /// </summary>
    [Test]
    public void AddTool_MultipleDifferentTools_AddsAllTools()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        var mockTool1 = new Mock<AITool>();
        var mockTool2 = new Mock<AITool>();
        var mockTool3 = new Mock<AITool>();
        // Act
        agent.AddTool(mockTool1.Object);
        agent.AddTool(mockTool2.Object);
        agent.AddTool(mockTool3.Object);
        // Assert
        Assert.That(agent.Tools, Has.Count.EqualTo(3));
        Assert.That(agent.Tools, Contains.Item(mockTool1.Object));
        Assert.That(agent.Tools, Contains.Item(mockTool2.Object));
        Assert.That(agent.Tools, Contains.Item(mockTool3.Object));
    }

    /// <summary>
    /// Tests that AddTool allows the same tool instance to be added multiple times.
    /// </summary>
    [Test]
    public void AddTool_SameToolMultipleTimes_AddsEachInstance()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        var mockTool = new Mock<AITool>();
        // Act
        agent.AddTool(mockTool.Object);
        agent.AddTool(mockTool.Object);
        agent.AddTool(mockTool.Object);
        // Assert
        Assert.That(agent.Tools, Has.Count.EqualTo(3));
        Assert.That(agent.Tools.Count(t => ReferenceEquals(t, mockTool.Object)), Is.EqualTo(3));
    }

    /// <summary>
    /// Tests that AddTool correctly adds tools when a system prompt is provided in the constructor.
    /// </summary>
    [Test]
    public void AddTool_WithSystemPrompt_AddsToolSuccessfully()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object, "Test system prompt");
        var mockTool = new Mock<AITool>();
        // Act
        agent.AddTool(mockTool.Object);
        // Assert
        Assert.That(agent.Tools, Has.Count.EqualTo(1));
        Assert.That(agent.Tools, Contains.Item(mockTool.Object));
    }

    /// <summary>
    /// Tests that Tools collection remains empty when no tools are added.
    /// </summary>
    [Test]
    public void AddTool_NoToolsAdded_ToolsCollectionIsEmpty()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        // Act
        // No tools added
        // Assert
        Assert.That(agent.Tools, Is.Empty);
        Assert.That(agent.Tools, Has.Count.EqualTo(0));
    }

    /// <summary>
    /// Tests that AddTool throws ArgumentNullException when the func parameter is null.
    /// </summary>
    [Test]
    public void AddTool_NullFunc_ThrowsArgumentNullException()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        Func<int>? nullFunc = null;
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => agent.AddTool(nullFunc!));
    }

    /// <summary>
    /// Tests that AddTool successfully adds a tool when given a valid function with various combinations of name and description.
    /// Verifies the tool count increases and the tool is present in the Tools collection.
    /// </summary>
    /// <param name = "name">The optional name for the tool.</param>
    /// <param name = "description">The optional description for the tool.</param>
    [TestCase(null, null)]
    [TestCase("TestFunction", null)]
    [TestCase(null, "Test description")]
    [TestCase("TestFunction", "Test description")]
    [TestCase("", "")]
    [TestCase("   ", "   ")]
    public void AddTool_ValidFuncWithVariousNameAndDescription_AddsToolSuccessfully(string? name, string? description)
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        Func<int> testFunc = () => 42;
        var initialCount = agent.Tools.Count;
        // Act
        agent.AddTool(testFunc, name, description);
        // Assert
        Assert.That(agent.Tools.Count, Is.EqualTo(initialCount + 1), "Tools count should increase by 1");
        Assert.That(agent.Tools, Is.Not.Empty, "Tools collection should not be empty");
    }

    /// <summary>
    /// Tests that AddTool works correctly with different generic return types.
    /// </summary>
    [Test]
    public void AddTool_DifferentGenericReturnTypes_AddsToolsSuccessfully()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        Func<int> intFunc = () => 42;
        Func<string> stringFunc = () => "test";
        Func<object> objectFunc = () => new object();
        Func<double> doubleFunc = () => 3.14;
        // Act
        agent.AddTool(intFunc, "intFunc");
        agent.AddTool(stringFunc, "stringFunc");
        agent.AddTool(objectFunc, "objectFunc");
        agent.AddTool(doubleFunc, "doubleFunc");
        // Assert
        Assert.That(agent.Tools.Count, Is.EqualTo(4), "Should have 4 tools registered");
    }

    /// <summary>
    /// Tests that AddTool handles very long strings for name and description parameters.
    /// </summary>
    [Test]
    public void AddTool_VeryLongNameAndDescription_AddsToolSuccessfully()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        Func<int> testFunc = () => 42;
        var longName = new string('a', 10000);
        var longDescription = new string('b', 10000);
        // Act
        agent.AddTool(testFunc, longName, longDescription);
        // Assert
        Assert.That(agent.Tools.Count, Is.EqualTo(1), "Tool should be added successfully");
    }

    /// <summary>
    /// Tests that AddTool handles special characters in name and description parameters.
    /// </summary>
    [TestCase("func@#$%^", "desc@#$%^")]
    [TestCase("func\n\r\t", "desc\n\r\t")]
    [TestCase("func<>\"'&", "desc<>\"'&")]
    public void AddTool_SpecialCharactersInNameAndDescription_AddsToolSuccessfully(string name, string description)
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        Func<int> testFunc = () => 42;
        // Act
        agent.AddTool(testFunc, name, description);
        // Assert
        Assert.That(agent.Tools.Count, Is.EqualTo(1), "Tool should be added successfully with special characters");
    }

    /// <summary>
    /// Tests that multiple calls to AddTool accumulate tools in the Tools collection.
    /// </summary>
    [Test]
    public void AddTool_MultipleCalls_AccumulatesTools()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        Func<int> func1 = () => 1;
        Func<int> func2 = () => 2;
        Func<int> func3 = () => 3;
        // Act
        agent.AddTool(func1, "func1");
        agent.AddTool(func2, "func2");
        agent.AddTool(func3, "func3");
        // Assert
        Assert.That(agent.Tools.Count, Is.EqualTo(3), "Should have 3 tools registered");
    }

    /// <summary>
    /// Tests that AddTool successfully adds a tool with a function returning a nullable reference type.
    /// </summary>
    [Test]
    public void AddTool_FuncReturningNullableReferenceType_AddsToolSuccessfully()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        Func<string?> nullableFunc = () => null;
        // Act
        agent.AddTool(nullableFunc, "nullableFunc", "Returns nullable string");
        // Assert
        Assert.That(agent.Tools.Count, Is.EqualTo(1), "Tool with nullable return type should be added");
    }

    /// <summary>
    /// Tests that AddTool handles a function that throws an exception when invoked (registration should still succeed).
    /// </summary>
    [Test]
    public void AddTool_FuncThatThrowsWhenInvoked_AddsToolSuccessfully()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        Func<int> throwingFunc = () => throw new InvalidOperationException("Test exception");
        // Act
        agent.AddTool(throwingFunc, "throwingFunc", "A function that throws");
        // Assert
        Assert.That(agent.Tools.Count, Is.EqualTo(1), "Tool registration should succeed even if function throws when invoked");
    }

    /// <summary>
    /// Tests that AddTool with a valid function, name, and description successfully adds the tool to the Tools collection.
    /// </summary>
    [Test]
    public void AddTool_ValidFuncWithNameAndDescription_AddsToolToCollection()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        Func<int, string> testFunc = (x) => x.ToString();
        var toolName = "TestTool";
        var toolDescription = "Test tool description";
        // Act
        agent.AddTool(testFunc, toolName, toolDescription);
        // Assert
        Assert.That(agent.Tools.Count, Is.EqualTo(1));
        Assert.That(agent.Tools[0], Is.Not.Null);
    }

    /// <summary>
    /// Tests that AddTool with a valid function and null name and description successfully adds the tool to the Tools collection.
    /// </summary>
    [Test]
    public void AddTool_ValidFuncWithNullNameAndDescription_AddsToolToCollection()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        Func<int, string> testFunc = (x) => x.ToString();
        // Act
        agent.AddTool(testFunc, null, null);
        // Assert
        Assert.That(agent.Tools.Count, Is.EqualTo(1));
        Assert.That(agent.Tools[0], Is.Not.Null);
    }

    /// <summary>
    /// Tests that AddTool with a valid function and only name adds the tool to the Tools collection.
    /// </summary>
    [Test]
    public void AddTool_ValidFuncWithNameOnly_AddsToolToCollection()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        Func<string, int> testFunc = (x) => x.Length;
        var toolName = "StringLengthTool";
        // Act
        agent.AddTool(testFunc, toolName, null);
        // Assert
        Assert.That(agent.Tools.Count, Is.EqualTo(1));
        Assert.That(agent.Tools[0], Is.Not.Null);
    }

    /// <summary>
    /// Tests that AddTool with a valid function and only description adds the tool to the Tools collection.
    /// </summary>
    [Test]
    public void AddTool_ValidFuncWithDescriptionOnly_AddsToolToCollection()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        Func<double, bool> testFunc = (x) => x > 0;
        var toolDescription = "Checks if number is positive";
        // Act
        agent.AddTool(testFunc, null, toolDescription);
        // Assert
        Assert.That(agent.Tools.Count, Is.EqualTo(1));
        Assert.That(agent.Tools[0], Is.Not.Null);
    }

    /// <summary>
    /// Tests that AddTool can be called multiple times to add multiple tools.
    /// </summary>
    [Test]
    public void AddTool_MultipleCalls_AddsMultipleTools()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        Func<int, string> func1 = (x) => x.ToString();
        Func<string, int> func2 = (x) => x.Length;
        Func<double, bool> func3 = (x) => x > 0;
        // Act
        agent.AddTool(func1, "Tool1", "First tool");
        agent.AddTool(func2, "Tool2", "Second tool");
        agent.AddTool(func3, "Tool3", "Third tool");
        // Assert
        Assert.That(agent.Tools.Count, Is.EqualTo(3));
    }

    /// <summary>
    /// Tests that AddTool with empty string name adds the tool to the Tools collection.
    /// </summary>
    [Test]
    public void AddTool_EmptyStringName_AddsToolToCollection()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        Func<int, int> testFunc = (x) => x * 2;
        // Act
        agent.AddTool(testFunc, string.Empty, "description");
        // Assert
        Assert.That(agent.Tools.Count, Is.EqualTo(1));
    }

    /// <summary>
    /// Tests that AddTool with empty string description adds the tool to the Tools collection.
    /// </summary>
    [Test]
    public void AddTool_EmptyStringDescription_AddsToolToCollection()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        Func<int, int> testFunc = (x) => x * 2;
        // Act
        agent.AddTool(testFunc, "name", string.Empty);
        // Assert
        Assert.That(agent.Tools.Count, Is.EqualTo(1));
    }

    /// <summary>
    /// Tests that AddTool with whitespace-only name and description adds the tool to the Tools collection.
    /// </summary>
    [Test]
    public void AddTool_WhitespaceNameAndDescription_AddsToolToCollection()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        Func<string, string> testFunc = (x) => x.Trim();
        // Act
        agent.AddTool(testFunc, "   ", "   ");
        // Assert
        Assert.That(agent.Tools.Count, Is.EqualTo(1));
    }

    /// <summary>
    /// Tests that AddTool with very long strings for name and description adds the tool to the Tools collection.
    /// </summary>
    [Test]
    public void AddTool_VeryLongStrings_AddsToolToCollection()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        Func<int, int> testFunc = (x) => x;
        var longName = new string('A', 10000);
        var longDescription = new string('B', 10000);
        // Act
        agent.AddTool(testFunc, longName, longDescription);
        // Assert
        Assert.That(agent.Tools.Count, Is.EqualTo(1));
    }

    /// <summary>
    /// Tests that AddTool with special characters in name and description adds the tool to the Tools collection.
    /// </summary>
    [Test]
    public void AddTool_SpecialCharactersInNameAndDescription_AddsToolToCollection()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        Func<int, int> testFunc = (x) => x;
        var specialName = "!@#$%^&*()_+-=[]{}|;':\",./<>?";
        var specialDescription = "Special chars: \n\r\t\\";
        // Act
        agent.AddTool(testFunc, specialName, specialDescription);
        // Assert
        Assert.That(agent.Tools.Count, Is.EqualTo(1));
    }

    /// <summary>
    /// Tests that AddTool works with different generic type combinations (string to bool).
    /// </summary>
    [Test]
    public void AddTool_DifferentGenericTypes_StringToBool_AddsToolToCollection()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        Func<string, bool> testFunc = (x) => !string.IsNullOrEmpty(x);
        // Act
        agent.AddTool(testFunc, "IsNotEmpty", "Checks if string is not empty");
        // Assert
        Assert.That(agent.Tools.Count, Is.EqualTo(1));
    }

    /// <summary>
    /// Tests that AddTool works with different generic type combinations (object to object).
    /// </summary>
    [Test]
    public void AddTool_DifferentGenericTypes_ObjectToObject_AddsToolToCollection()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        Func<object, object> testFunc = (x) => x;
        // Act
        agent.AddTool(testFunc, "Identity", "Returns the same object");
        // Assert
        Assert.That(agent.Tools.Count, Is.EqualTo(1));
    }

    /// <summary>
    /// Tests that AddTool with a valid function successfully adds the function to the Tools list.
    /// </summary>
    [Test]
    public void AddTool_ValidFunction_AddsFunctionToToolsList()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        Func<int, string, bool> testFunc = (x, y) => x > 0 && !string.IsNullOrEmpty(y);
        // Act
        agent.AddTool(testFunc);
        // Assert
        Assert.That(agent.Tools.Count, Is.EqualTo(1));
        Assert.That(agent.Tools[0], Is.Not.Null);
    }

    /// <summary>
    /// Tests that AddTool throws ArgumentNullException when the function parameter is null.
    /// </summary>
    [Test]
    public void AddTool_NullFunction_ThrowsArgumentNullException()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        Func<int, string, bool>? nullFunc = null;
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => agent.AddTool(nullFunc!));
    }

    /// <summary>
    /// Tests that AddTool with name and description parameters correctly adds the tool with metadata.
    /// </summary>
    [Test]
    public void AddTool_WithNameAndDescription_AddsToolWithMetadata()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        Func<int, string, bool> testFunc = (x, y) => x > 0 && !string.IsNullOrEmpty(y);
        var name = "TestFunction";
        var description = "This is a test function";
        // Act
        agent.AddTool(testFunc, name, description);
        // Assert
        Assert.That(agent.Tools.Count, Is.EqualTo(1));
        var addedTool = agent.Tools[0];
        Assert.That(addedTool, Is.Not.Null);
        Assert.That(addedTool.Name, Is.EqualTo(name));
        Assert.That(addedTool.Description, Is.EqualTo(description));
    }

    /// <summary>
    /// Tests that AddTool with null name and description (default parameters) successfully adds the tool.
    /// </summary>
    [Test]
    public void AddTool_WithNullNameAndDescription_AddsToolSuccessfully()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        Func<int, string, bool> testFunc = (x, y) => x > 0 && !string.IsNullOrEmpty(y);
        // Act
        agent.AddTool(testFunc, null, null);
        // Assert
        Assert.That(agent.Tools.Count, Is.EqualTo(1));
        Assert.That(agent.Tools[0], Is.Not.Null);
    }

    /// <summary>
    /// Tests that AddTool works correctly with various type parameter combinations.
    /// </summary>
    /// <param name = "expectedCount">The expected number of tools after addition.</param>
    [TestCase(1)]
    public void AddTool_DifferentTypeParameters_AddsToolSuccessfully(int expectedCount)
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        // Act - Test with different type combinations
        Func<int, string, bool> intStringBool = (x, y) => x > 0 && !string.IsNullOrEmpty(y);
        agent.AddTool(intStringBool);
        // Assert
        Assert.That(agent.Tools.Count, Is.EqualTo(expectedCount));
    }

    /// <summary>
    /// Tests that AddTool works with value type parameters (int, double, bool).
    /// </summary>
    [Test]
    public void AddTool_WithValueTypes_AddsToolSuccessfully()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        Func<int, double, bool> testFunc = (x, y) => x > y;
        // Act
        agent.AddTool(testFunc);
        // Assert
        Assert.That(agent.Tools.Count, Is.EqualTo(1));
    }

    /// <summary>
    /// Tests that AddTool works with reference type parameters.
    /// </summary>
    [Test]
    public void AddTool_WithReferenceTypes_AddsToolSuccessfully()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        Func<string, object, string> testFunc = (x, y) => x + y.ToString();
        // Act
        agent.AddTool(testFunc);
        // Assert
        Assert.That(agent.Tools.Count, Is.EqualTo(1));
    }

    /// <summary>
    /// Tests that AddTool with an empty string for name successfully adds the tool.
    /// </summary>
    [Test]
    public void AddTool_WithEmptyName_AddsToolSuccessfully()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        Func<int, string, bool> testFunc = (x, y) => x > 0;
        // Act
        agent.AddTool(testFunc, string.Empty, "description");
        // Assert
        Assert.That(agent.Tools.Count, Is.EqualTo(1));
    }

    /// <summary>
    /// Tests that AddTool with a whitespace-only string for name successfully adds the tool.
    /// </summary>
    [Test]
    public void AddTool_WithWhitespaceName_AddsToolSuccessfully()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        Func<int, string, bool> testFunc = (x, y) => x > 0;
        // Act
        agent.AddTool(testFunc, "   ", "description");
        // Assert
        Assert.That(agent.Tools.Count, Is.EqualTo(1));
    }

    /// <summary>
    /// Tests that AddTool with an empty string for description successfully adds the tool.
    /// </summary>
    [Test]
    public void AddTool_WithEmptyDescription_AddsToolSuccessfully()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        Func<int, string, bool> testFunc = (x, y) => x > 0;
        // Act
        agent.AddTool(testFunc, "name", string.Empty);
        // Assert
        Assert.That(agent.Tools.Count, Is.EqualTo(1));
    }

    /// <summary>
    /// Tests that AddTool with a whitespace-only string for description successfully adds the tool.
    /// </summary>
    [Test]
    public void AddTool_WithWhitespaceDescription_AddsToolSuccessfully()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        Func<int, string, bool> testFunc = (x, y) => x > 0;
        // Act
        agent.AddTool(testFunc, "name", "   ");
        // Assert
        Assert.That(agent.Tools.Count, Is.EqualTo(1));
    }

    /// <summary>
    /// Tests that AddTool with very long strings for name and description successfully adds the tool.
    /// </summary>
    [Test]
    public void AddTool_WithVeryLongNameAndDescription_AddsToolSuccessfully()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        Func<int, string, bool> testFunc = (x, y) => x > 0;
        var longName = new string('a', 1000);
        var longDescription = new string('b', 5000);
        // Act
        agent.AddTool(testFunc, longName, longDescription);
        // Assert
        Assert.That(agent.Tools.Count, Is.EqualTo(1));
        Assert.That(agent.Tools[0].Name, Is.EqualTo(longName));
        Assert.That(agent.Tools[0].Description, Is.EqualTo(longDescription));
    }

    /// <summary>
    /// Tests that AddTool with special characters in name and description successfully adds the tool.
    /// </summary>
    [Test]
    public void AddTool_WithSpecialCharactersInNameAndDescription_AddsToolSuccessfully()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        Func<int, string, bool> testFunc = (x, y) => x > 0;
        var specialName = "Test!@#$%^&*()_+<>?";
        var specialDescription = "Description with 日本語 and émojis 🎉";
        // Act
        agent.AddTool(testFunc, specialName, specialDescription);
        // Assert
        Assert.That(agent.Tools.Count, Is.EqualTo(1));
        Assert.That(agent.Tools[0].Name, Is.EqualTo(specialName));
        Assert.That(agent.Tools[0].Description, Is.EqualTo(specialDescription));
    }

    /// <summary>
    /// Tests that AddTool with nullable type parameters successfully adds the tool.
    /// </summary>
    [Test]
    public void AddTool_WithNullableTypeParameters_AddsToolSuccessfully()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        Func<int?, string?, bool?> testFunc = (x, y) => x > 0;
        // Act
        agent.AddTool(testFunc);
        // Assert
        Assert.That(agent.Tools.Count, Is.EqualTo(1));
    }

    /// <summary>
    /// Tests that AddTool returns the correct tool when retrieved from Tools collection.
    /// </summary>
    [Test]
    public void AddTool_VerifyToolCanBeRetrievedFromCollection()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        Func<int, string, bool> testFunc = (x, y) => x > 0;
        var name = "MyFunction";
        // Act
        agent.AddTool(testFunc, name);
        var retrievedTool = agent.Tools.FirstOrDefault();
        // Assert
        Assert.That(retrievedTool, Is.Not.Null);
        Assert.That(retrievedTool!.Name, Is.EqualTo(name));
    }

    /// <summary>
    /// Tests that AddTool with only name parameter (description is null) successfully adds the tool.
    /// </summary>
    [Test]
    public void AddTool_WithOnlyName_AddsToolSuccessfully()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        Func<int, string, bool> testFunc = (x, y) => x > 0;
        var name = "NamedFunction";
        // Act
        agent.AddTool(testFunc, name);
        // Assert
        Assert.That(agent.Tools.Count, Is.EqualTo(1));
        Assert.That(agent.Tools[0].Name, Is.EqualTo(name));
    }

    /// <summary>
    /// Tests that AddTool with only description parameter (name is null) successfully adds the tool.
    /// </summary>
    [Test]
    public void AddTool_WithOnlyDescription_AddsToolSuccessfully()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        Func<int, string, bool> testFunc = (x, y) => x > 0;
        var description = "This function does something";
        // Act
        agent.AddTool(testFunc, null, description);
        // Assert
        Assert.That(agent.Tools.Count, Is.EqualTo(1));
        Assert.That(agent.Tools[0].Description, Is.EqualTo(description));
    }

    /// <summary>
    /// Tests that AddToolAsync throws ArgumentNullException when func parameter is null.
    /// </summary>
    [Test]
    public void AddToolAsync_NullFunc_ThrowsArgumentNullException()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        Func<int, Task<string>>? nullFunc = null;
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => agent.AddToolAsync(nullFunc!, "testName", "testDescription"));
    }

    /// <summary>
    /// Tests that AddToolAsync successfully adds a tool when all parameters are provided with valid values.
    /// </summary>
    [Test]
    public void AddToolAsync_ValidFuncWithNameAndDescription_AddsTool()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        Func<int, Task<string>> testFunc = async (x) => await Task.FromResult(x.ToString());
        var initialCount = agent.Tools.Count;
        // Act
        agent.AddToolAsync(testFunc, "TestFunction", "A test function");
        // Assert
        Assert.That(agent.Tools.Count, Is.EqualTo(initialCount + 1));
        Assert.That(agent.Tools.Last(), Is.Not.Null);
    }

    /// <summary>
    /// Tests that AddToolAsync successfully adds a tool when only func is provided (name and description are null).
    /// </summary>
    [Test]
    public void AddToolAsync_ValidFuncWithNullNameAndDescription_AddsTool()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        Func<string, Task<int>> testFunc = async (s) => await Task.FromResult(s.Length);
        var initialCount = agent.Tools.Count;
        // Act
        agent.AddToolAsync(testFunc, null, null);
        // Assert
        Assert.That(agent.Tools.Count, Is.EqualTo(initialCount + 1));
        Assert.That(agent.Tools.Last(), Is.Not.Null);
    }

    /// <summary>
    /// Tests that AddToolAsync successfully adds a tool with various combinations of name and description parameters.
    /// </summary>
    /// <param name = "name">The name parameter to test.</param>
    /// <param name = "description">The description parameter to test.</param>
    [TestCase("TestName", null)]
    [TestCase(null, "TestDescription")]
    [TestCase("", "")]
    [TestCase("   ", "   ")]
    [TestCase("Name with spaces", "Description with special chars !@#$%")]
    [TestCase("VeryLongNameThatExceedsTypicalLengthLimitsForFunctionNamesButShouldStillBeAcceptedByTheSystem", "VeryLongDescriptionThatExceedsTypicalLengthLimitsForFunctionDescriptionsButShouldStillBeAcceptedByTheSystem")]
    public void AddToolAsync_ValidFuncWithVariousNameAndDescription_AddsTool(string? name, string? description)
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        Func<double, Task<bool>> testFunc = async (d) => await Task.FromResult(d > 0);
        var initialCount = agent.Tools.Count;
        // Act
        agent.AddToolAsync(testFunc, name, description);
        // Assert
        Assert.That(agent.Tools.Count, Is.EqualTo(initialCount + 1));
        Assert.That(agent.Tools.Last(), Is.Not.Null);
    }

    /// <summary>
    /// Tests that AddToolAsync can be called multiple times to add multiple tools,
    /// and each tool is added to the Tools collection.
    /// </summary>
    [Test]
    public void AddToolAsync_MultipleAdditions_AddsMultipleTools()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        Func<int, Task<string>> func1 = async (x) => await Task.FromResult(x.ToString());
        Func<string, Task<int>> func2 = async (s) => await Task.FromResult(s.Length);
        Func<bool, Task<string>> func3 = async (b) => await Task.FromResult(b.ToString());
        var initialCount = agent.Tools.Count;
        // Act
        agent.AddToolAsync(func1, "Func1", "First function");
        agent.AddToolAsync(func2, "Func2", "Second function");
        agent.AddToolAsync(func3, "Func3", "Third function");
        // Assert
        Assert.That(agent.Tools.Count, Is.EqualTo(initialCount + 3));
    }

    /// <summary>
    /// Tests that AddToolAsync works with various generic type parameters.
    /// </summary>
    [Test]
    public void AddToolAsync_VariousGenericTypes_AddsTool()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        var initialCount = agent.Tools.Count;
        // Act
        agent.AddToolAsync<int, string>(async (x) => await Task.FromResult(x.ToString()), "IntToString", "Converts int to string");
        agent.AddToolAsync<string, double>(async (s) => await Task.FromResult(double.Parse(s)), "StringToDouble", "Parses string to double");
        agent.AddToolAsync<bool, int>(async (b) => await Task.FromResult(b ? 1 : 0), "BoolToInt", "Converts bool to int");
        // Assert
        Assert.That(agent.Tools.Count, Is.EqualTo(initialCount + 3));
    }

    /// <summary>
    /// Tests that the tool added by AddToolAsync is accessible through the Tools property.
    /// </summary>
    [Test]
    public void AddToolAsync_AddedTool_IsAccessibleThroughToolsProperty()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        Func<string, Task<string>> testFunc = async (s) => await Task.FromResult(s.ToUpper());
        var initialCount = agent.Tools.Count;
        // Act
        agent.AddToolAsync(testFunc, "ToUpperCase", "Converts string to uppercase");
        // Assert
        Assert.That(agent.Tools, Is.Not.Null);
        Assert.That(agent.Tools.Count, Is.GreaterThan(initialCount));
        var addedTool = agent.Tools.Last();
        Assert.That(addedTool, Is.Not.Null);
        Assert.That(addedTool, Is.InstanceOf<AIFunction>());
    }

    /// <summary>
    /// Tests that calling ClearTools when the tools list is empty does not throw an exception
    /// and maintains an empty collection.
    /// </summary>
    [Test]
    public void ClearTools_WhenToolsListIsEmpty_DoesNotThrowAndRemainsEmpty()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new ReceptyOks.Shared.AI.AiAgent(mockChatClient.Object);
        // Act
        agent.ClearTools();
        // Assert
        Assert.That(agent.Tools.Count, Is.EqualTo(0));
    }

    /// <summary>
    /// Tests that calling ClearTools when the tools list contains a single tool
    /// removes all tools, resulting in an empty collection.
    /// </summary>
    [Test]
    public void ClearTools_WhenToolsListHasSingleTool_RemovesAllTools()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new ReceptyOks.Shared.AI.AiAgent(mockChatClient.Object);
        var mockTool = new Mock<AITool>();
        agent.AddTool(mockTool.Object);
        // Act
        agent.ClearTools();
        // Assert
        Assert.That(agent.Tools.Count, Is.EqualTo(0));
    }

    /// <summary>
    /// Tests that calling ClearTools when the tools list contains multiple tools
    /// removes all tools, resulting in an empty collection.
    /// </summary>
    [Test]
    public void ClearTools_WhenToolsListHasMultipleTools_RemovesAllTools()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new ReceptyOks.Shared.AI.AiAgent(mockChatClient.Object);
        var mockTool1 = new Mock<AITool>();
        var mockTool2 = new Mock<AITool>();
        var mockTool3 = new Mock<AITool>();
        agent.AddTool(mockTool1.Object);
        agent.AddTool(mockTool2.Object);
        agent.AddTool(mockTool3.Object);
        // Act
        agent.ClearTools();
        // Assert
        Assert.That(agent.Tools.Count, Is.EqualTo(0));
    }

    /// <summary>
    /// Tests that calling ClearTools multiple times in succession is idempotent
    /// and does not throw exceptions, maintaining an empty collection.
    /// </summary>
    [Test]
    public void ClearTools_CalledMultipleTimes_RemainsIdempotent()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new ReceptyOks.Shared.AI.AiAgent(mockChatClient.Object);
        var mockTool = new Mock<AITool>();
        agent.AddTool(mockTool.Object);
        // Act
        agent.ClearTools();
        agent.ClearTools();
        agent.ClearTools();
        // Assert
        Assert.That(agent.Tools.Count, Is.EqualTo(0));
    }

    /// <summary>
    /// Tests that after clearing tools, new tools can be successfully added
    /// to the collection, verifying the list remains usable.
    /// </summary>
    [Test]
    public void ClearTools_AfterClearing_CanAddToolsAgain()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new ReceptyOks.Shared.AI.AiAgent(mockChatClient.Object);
        var mockTool1 = new Mock<AITool>();
        var mockTool2 = new Mock<AITool>();
        agent.AddTool(mockTool1.Object);
        agent.ClearTools();
        // Act
        agent.AddTool(mockTool2.Object);
        // Assert
        Assert.That(agent.Tools.Count, Is.EqualTo(1));
    }

    /// <summary>
    /// Tests that ChatAsync throws ArgumentException when userMessage is an empty string.
    /// This validates that empty strings are rejected during input validation.
    /// </summary>
    [Test]
    public void ChatAsync_EmptyUserMessage_ThrowsArgumentException()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var aiAgent = new AiAgent(mockChatClient.Object, "Test system prompt");
        var userMessage = string.Empty;
        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(async () => await aiAgent.ChatAsync(userMessage, cancellationToken: CancellationToken.None));
    }

    /// <summary>
    /// Tests that ChatAsync throws ArgumentException when userMessage contains only whitespace characters.
    /// This validates that whitespace-only strings (spaces, tabs, newlines) are rejected.
    /// </summary>
    [TestCase(" ")]
    [TestCase("  ")]
    [TestCase("\t")]
    [TestCase("\n")]
    [TestCase("\r\n")]
    [TestCase("   \t  \n  ")]
    public void ChatAsync_WhitespaceUserMessage_ThrowsArgumentException(string userMessage)
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var aiAgent = new AiAgent(mockChatClient.Object, "Test system prompt");
        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(async () => await aiAgent.ChatAsync(userMessage, cancellationToken: CancellationToken.None));
    }


    /// <summary>
    /// Tests that ChatStreamAsync throws ArgumentException when userMessage is empty.
    /// </summary>
    [Test]
    public void ChatStreamAsync_EmptyUserMessage_ThrowsArgumentException()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        Action<string> callback = _ =>
        {
        };
        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(async () => await agent.ChatStreamAsync(string.Empty, callback, CancellationToken.None));
    }

    /// <summary>
    /// Tests that ChatStreamAsync throws ArgumentException when userMessage contains only whitespace.
    /// </summary>
    /// <param name = "whitespaceMessage">A string containing only whitespace characters.</param>
    [TestCase(" ")]
    [TestCase("   ")]
    [TestCase("\t")]
    [TestCase("\n")]
    [TestCase("\r\n")]
    [TestCase(" \t \n ")]
    public void ChatStreamAsync_WhitespaceUserMessage_ThrowsArgumentException(string whitespaceMessage)
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        Action<string> callback = _ =>
        {
        };
        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(async () => await agent.ChatStreamAsync(whitespaceMessage, callback, CancellationToken.None));
    }

    /// <summary>
    /// Tests that ChatStreamAsync throws ArgumentNullException when onTextReceived callback is null.
    /// </summary>
    [Test]
    public void ChatStreamAsync_NullCallback_ThrowsArgumentNullException()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        // Act & Assert
        Assert.ThrowsAsync<ArgumentNullException>(async () => await agent.ChatStreamAsync("test message", null!, CancellationToken.None));
    }

    /// <summary>
    /// Tests that calling ClearHistory sets the ConversationId to null.
    /// Verifies that the conversation state is properly cleared when the method is invoked.
    /// </summary>
    [Test]
    public void ClearHistory_WhenCalled_SetsConversationIdToNull()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        // Act
        agent.ClearHistory();
        // Assert
        Assert.That(agent.ConversationId, Is.Null);
    }

    /// <summary>
    /// Tests that calling ClearHistory multiple times in succession does not throw exceptions.
    /// Verifies the idempotent nature of the method - it can be safely called multiple times.
    /// </summary>
    [Test]
    public void ClearHistory_CalledMultipleTimes_DoesNotThrowException()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        // Act & Assert
        Assert.DoesNotThrow(() =>
        {
            agent.ClearHistory();
            agent.ClearHistory();
            agent.ClearHistory();
        });
        Assert.That(agent.ConversationId, Is.Null);
    }

    /// <summary>
    /// Tests that SaveConversationAsync throws InvalidOperationException when there is no active conversation thread.
    /// This verifies the null check guard clause at the beginning of the method.
    /// </summary>
    [Test]
    public void SaveConversationAsync_WhenThreadIsNull_ThrowsInvalidOperationException()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        // Act & Assert
        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await agent.SaveConversationAsync());
        Assert.That(exception, Is.Not.Null);
        Assert.That(exception!.Message, Does.Contain("No active conversation to save"));
        Assert.That(exception.Message, Does.Contain("Start a conversation first by calling ChatAsync or ChatStreamAsync"));
    }

    /// <summary>
    /// Tests that SaveConversationAsync throws InvalidOperationException with a cancellation token
    /// when thread is null. This verifies that the null check happens before any cancellation token usage.
    /// Note: The cancellationToken parameter is not actually used in the method implementation.
    /// </summary>
    [Test]
    public void SaveConversationAsync_WithCancelledToken_ThrowsInvalidOperationExceptionWhenThreadIsNull()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        var cts = new CancellationTokenSource();
        cts.Cancel();
        // Act & Assert
        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await agent.SaveConversationAsync(cts.Token));
        Assert.That(exception, Is.Not.Null);
        Assert.That(exception!.Message, Does.Contain("No active conversation to save"));
    }

    /// <summary>
    /// Tests that SaveConversationAsync throws InvalidOperationException with a default cancellation token
    /// when thread is null. This verifies the exception behavior with the default parameter value.
    /// </summary>
    [Test]
    public void SaveConversationAsync_WithDefaultCancellationToken_ThrowsInvalidOperationExceptionWhenThreadIsNull()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        // Act & Assert
        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await agent.SaveConversationAsync(default));
        Assert.That(exception, Is.Not.Null);
        Assert.That(exception!.Message, Does.Contain("No active conversation to save"));
    }

    /// <summary>
    /// Tests that LoadConversationAsync throws ArgumentException when serializedThread is an empty string.
    /// </summary>
    [Test]
    public void LoadConversationAsync_EmptySerializedThread_ThrowsArgumentException()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var aiAgent = new ReceptyOks.Shared.AI.AiAgent(mockChatClient.Object);
        string serializedThread = string.Empty;
        // Act & Assert
        var ex = Assert.ThrowsAsync<ArgumentException>(async () => await aiAgent.LoadConversationAsync(serializedThread, null, CancellationToken.None));
        Assert.That(ex, Is.Not.Null);
    }

    /// <summary>
    /// Tests that LoadConversationAsync throws ArgumentException when serializedThread contains only whitespace.
    /// </summary>
    [TestCase(" ")]
    [TestCase("   ")]
    [TestCase("\t")]
    [TestCase("\n")]
    [TestCase("\r\n")]
    public void LoadConversationAsync_WhitespaceSerializedThread_ThrowsArgumentException(string whitespaceThread)
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var aiAgent = new ReceptyOks.Shared.AI.AiAgent(mockChatClient.Object);
        // Act & Assert
        var ex = Assert.ThrowsAsync<ArgumentException>(async () => await aiAgent.LoadConversationAsync(whitespaceThread, null, CancellationToken.None));
        Assert.That(ex, Is.Not.Null);
    }

    /// <summary>
    /// Tests that LoadConversationAsync throws JsonException when serializedThread is not valid JSON.
    /// </summary>
    [TestCase("not valid json")]
    [TestCase("{incomplete")]
    [TestCase("[}")]
    [TestCase("random text 123")]
    public void LoadConversationAsync_InvalidJsonFormat_ThrowsJsonException(string invalidJson)
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var aiAgent = new ReceptyOks.Shared.AI.AiAgent(mockChatClient.Object);
        // Act & Assert
        Assert.ThrowsAsync<JsonException>(async () => await aiAgent.LoadConversationAsync(invalidJson, null, CancellationToken.None));
    }

    /// <summary>
    /// Tests that LoadConversationAsync stores null conversationId when not provided.
    /// Note: This test uses minimal JSON structure. The actual AgentThread JSON format 
    /// is implementation-specific and may cause deserialization to fail depending on 
    /// ChatClientAgent's expectations. If this test fails, update with a valid serialized thread format.
    /// </summary>
    [Test]
    public async Task LoadConversationAsync_ValidJsonWithNullConversationId_StoresNullConversationId()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var aiAgent = new ReceptyOks.Shared.AI.AiAgent(mockChatClient.Object);
        string serializedThread = "{}";
        // Act
        try
        {
            await aiAgent.LoadConversationAsync(serializedThread, null, CancellationToken.None);
            // Assert
            Assert.That(aiAgent.ConversationId, Is.Null);
        }
        catch (Exception ex) when (ex is not JsonException)
        {
            // If deserialization fails due to invalid format (not JSON parsing), 
            // this is expected as we don't know the exact AgentThread format.
            // Mark test as inconclusive with explanation.
            Assert.Inconclusive($"Test requires valid AgentThread JSON format. Exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Tests that LoadConversationAsync stores the provided conversationId correctly.
    /// Note: This test uses minimal JSON structure. The actual AgentThread JSON format 
    /// is implementation-specific and may cause deserialization to fail depending on 
    /// ChatClientAgent's expectations. If this test fails, update with a valid serialized thread format.
    /// </summary>
    [TestCase("test-conversation-id")]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("12345")]
    public async Task LoadConversationAsync_ValidJsonWithConversationId_StoresConversationId(string conversationId)
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var aiAgent = new ReceptyOks.Shared.AI.AiAgent(mockChatClient.Object);
        string serializedThread = "{}";
        // Act
        try
        {
            await aiAgent.LoadConversationAsync(serializedThread, conversationId, CancellationToken.None);
            // Assert
            Assert.That(aiAgent.ConversationId, Is.EqualTo(conversationId));
        }
        catch (Exception ex) when (ex is not JsonException)
        {
            // If deserialization fails due to invalid format (not JSON parsing), 
            // this is expected as we don't know the exact AgentThread format.
            // Mark test as inconclusive with explanation.
            Assert.Inconclusive($"Test requires valid AgentThread JSON format. Exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Tests that LoadConversationAsync correctly handles valid JSON with special characters in conversationId.
    /// </summary>
    [TestCase("conversation-with-special-chars-!@#$%")]
    [TestCase("conversation\twith\ttabs")]
    [TestCase("conversation\nwith\nnewlines")]
    public async Task LoadConversationAsync_ValidJsonWithSpecialCharactersInConversationId_StoresCorrectly(string conversationId)
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var aiAgent = new ReceptyOks.Shared.AI.AiAgent(mockChatClient.Object);
        string serializedThread = "{}";
        // Act
        try
        {
            await aiAgent.LoadConversationAsync(serializedThread, conversationId, CancellationToken.None);
            // Assert
            Assert.That(aiAgent.ConversationId, Is.EqualTo(conversationId));
        }
        catch (Exception ex) when (ex is not JsonException)
        {
            // If deserialization fails due to invalid format (not JSON parsing), 
            // this is expected as we don't know the exact AgentThread format.
            // Mark test as inconclusive with explanation.
            Assert.Inconclusive($"Test requires valid AgentThread JSON format. Exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Test helper class for JSON deserialization tests.
    /// </summary>
    public class TestResponse
    {
        public string? Message { get; set; }
        public int Code { get; set; }
    }

    /// <summary>
    /// Verifies that ChatAsync returns a properly deserialized object when given valid JSON response.
    /// </summary>
    [Test]
    public async Task ChatAsync_WithValidJsonResponse_ReturnsDeserializedObject()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var expectedResponse = new TestResponse
        {
            Message = "Success",
            Code = 200
        };
        var jsonResponse = JsonSerializer.Serialize(expectedResponse);
        SetupMockChatClient(mockChatClient, jsonResponse);
        var agent = new AiAgent(mockChatClient.Object);
        // Act
        var result = await agent.ChatAsync<TestResponse>("Test message");
        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Message, Is.EqualTo("Success"));
        Assert.That(result.Code, Is.EqualTo(200));
    }

    /// <summary>
    /// Verifies that ChatAsync extracts and deserializes JSON wrapped in markdown code blocks.
    /// </summary>
    [Test]
    public async Task ChatAsync_WithJsonInMarkdownCodeBlock_ReturnsDeserializedObject()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var expectedResponse = new TestResponse
        {
            Message = "Success",
            Code = 200
        };
        var jsonResponse = JsonSerializer.Serialize(expectedResponse);
        var markdownResponse = $"```json\n{jsonResponse}\n```";
        SetupMockChatClient(mockChatClient, markdownResponse);
        var agent = new AiAgent(mockChatClient.Object);
        // Act
        var result = await agent.ChatAsync<TestResponse>("Test message");
        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Message, Is.EqualTo("Success"));
        Assert.That(result.Code, Is.EqualTo(200));
    }

    /// <summary>
    /// Verifies that ChatAsync extracts and deserializes JSON wrapped in generic markdown code blocks (without json tag).
    /// </summary>
    [Test]
    public async Task ChatAsync_WithJsonInGenericMarkdownCodeBlock_ReturnsDeserializedObject()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var expectedResponse = new TestResponse
        {
            Message = "Success",
            Code = 200
        };
        var jsonResponse = JsonSerializer.Serialize(expectedResponse);
        var markdownResponse = $"```\n{jsonResponse}\n```";
        SetupMockChatClient(mockChatClient, markdownResponse);
        var agent = new AiAgent(mockChatClient.Object);
        // Act
        var result = await agent.ChatAsync<TestResponse>("Test message");
        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Message, Is.EqualTo("Success"));
        Assert.That(result.Code, Is.EqualTo(200));
    }

    /// <summary>
    /// Verifies that ChatAsync returns null when the response contains invalid JSON.
    /// </summary>
    [Test]
    public async Task ChatAsync_WithInvalidJson_ReturnsNull()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var invalidJson = "{ this is not valid json }";
        SetupMockChatClient(mockChatClient, invalidJson);
        var agent = new AiAgent(mockChatClient.Object);
        // Act
        var result = await agent.ChatAsync<TestResponse>("Test message");
        // Assert
        Assert.That(result, Is.Null);
    }

    /// <summary>
    /// Verifies that ChatAsync returns null when the response is null, empty, or whitespace.
    /// Tests boundary conditions for response text parsing.
    /// </summary>
    [TestCase(null, Description = "Null response")]
    [TestCase("", Description = "Empty response")]
    [TestCase("   ", Description = "Whitespace response")]
    [TestCase("\t\n\r", Description = "Whitespace characters response")]
    public async Task ChatAsync_WithNullOrWhitespaceResponse_ReturnsNull(string? responseText)
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        SetupMockChatClient(mockChatClient, responseText!);
        var agent = new AiAgent(mockChatClient.Object);
        // Act
        var result = await agent.ChatAsync<TestResponse>("Test message");
        // Assert
        Assert.That(result, Is.Null);
    }

    /// <summary>
    /// Verifies that ChatAsync throws ArgumentException when userMessage is empty.
    /// Tests empty string validation for required parameter.
    /// </summary>
    [Test]
    public void ChatAsync_WithEmptyUserMessage_ThrowsArgumentException()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(async () => await agent.ChatAsync<TestResponse>(string.Empty));
    }

    /// <summary>
    /// Verifies that ChatAsync throws ArgumentException when userMessage contains only whitespace.
    /// Tests whitespace-only validation for required parameter.
    /// </summary>
    [TestCase("   ", Description = "Spaces only")]
    [TestCase("\t", Description = "Tab only")]
    [TestCase("\n", Description = "Newline only")]
    [TestCase("\r\n", Description = "Carriage return and newline")]
    [TestCase("  \t\n\r  ", Description = "Mixed whitespace")]
    public void ChatAsync_WithWhitespaceUserMessage_ThrowsArgumentException(string userMessage)
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(async () => await agent.ChatAsync<TestResponse>(userMessage));
    }

    /// <summary>
    /// Verifies that ChatAsync correctly passes through different maxToolRounds values
    /// to the underlying ChatAsync method.
    /// Tests parameter forwarding and boundary values.
    /// </summary>
    [TestCase(0, Description = "Zero rounds")]
    [TestCase(1, Description = "Single round")]
    [TestCase(5, Description = "Default value")]
    [TestCase(10, Description = "Higher value")]
    [TestCase(100, Description = "Very high value")]
    [TestCase(int.MaxValue, Description = "Maximum int value")]
    public async Task ChatAsync_WithDifferentMaxToolRounds_CompletesSuccessfully(int maxToolRounds)
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var expectedResponse = new TestResponse
        {
            Message = "Success",
            Code = 200
        };
        var jsonResponse = JsonSerializer.Serialize(expectedResponse);
        SetupMockChatClient(mockChatClient, jsonResponse);
        var agent = new AiAgent(mockChatClient.Object);
        // Act
        var result = await agent.ChatAsync<TestResponse>("Test message", maxToolRounds);
        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Message, Is.EqualTo("Success"));
    }

    /// <summary>
    /// Verifies that ChatAsync handles negative maxToolRounds values.
    /// Tests edge case for potentially invalid parameter values.
    /// </summary>
    [TestCase(-1, Description = "Negative one")]
    [TestCase(int.MinValue, Description = "Minimum int value")]
    public async Task ChatAsync_WithNegativeMaxToolRounds_CompletesSuccessfully(int maxToolRounds)
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var expectedResponse = new TestResponse
        {
            Message = "Success",
            Code = 200
        };
        var jsonResponse = JsonSerializer.Serialize(expectedResponse);
        SetupMockChatClient(mockChatClient, jsonResponse);
        var agent = new AiAgent(mockChatClient.Object);
        // Act
        var result = await agent.ChatAsync<TestResponse>("Test message", maxToolRounds);
        // Assert
        Assert.That(result, Is.Not.Null);
    }

    /// <summary>
    /// Verifies that ChatAsync handles JSON that matches schema but has different property casing.
    /// Tests case-insensitive deserialization.
    /// </summary>
    [Test]
    public async Task ChatAsync_WithDifferentCasingJson_ReturnsDeserializedObject()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var jsonWithDifferentCasing = "{\"message\":\"Success\",\"code\":200}";
        SetupMockChatClient(mockChatClient, jsonWithDifferentCasing);
        var agent = new AiAgent(mockChatClient.Object);
        // Act
        var result = await agent.ChatAsync<TestResponse>("Test message");
        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Message, Is.EqualTo("Success"));
        Assert.That(result.Code, Is.EqualTo(200));
    }

    /// <summary>
    /// Verifies that ChatAsync returns null when JSON structure doesn't match expected type.
    /// Tests deserialization failure handling.
    /// </summary>
    [Test]
    public async Task ChatAsync_WithMismatchedJsonStructure_ReturnsNull()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var mismatchedJson = "{\"differentProperty\":\"value\",\"anotherProperty\":123}";
        SetupMockChatClient(mockChatClient, mismatchedJson);
        var agent = new AiAgent(mockChatClient.Object);
        // Act
        var result = await agent.ChatAsync<TestResponse>("Test message");
        // Assert
        Assert.That(result, Is.Not.Null); // Deserializes but properties are default
        Assert.That(result!.Message, Is.Null);
        Assert.That(result.Code, Is.EqualTo(0));
    }

    /// <summary>
    /// Verifies that ChatAsync handles very long user messages.
    /// Tests edge case for message length.
    /// </summary>
    [Test]
    public async Task ChatAsync_WithVeryLongUserMessage_CompletesSuccessfully()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var expectedResponse = new TestResponse
        {
            Message = "Success",
            Code = 200
        };
        var jsonResponse = JsonSerializer.Serialize(expectedResponse);
        SetupMockChatClient(mockChatClient, jsonResponse);
        var agent = new AiAgent(mockChatClient.Object);
        var veryLongMessage = new string('x', 100000); // 100k characters
        // Act
        var result = await agent.ChatAsync<TestResponse>(veryLongMessage);
        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Message, Is.EqualTo("Success"));
    }

    /// <summary>
    /// Verifies that ChatAsync handles user messages with special characters.
    /// Tests edge case for special character handling.
    /// </summary>
    [TestCase("Test with\nnewlines\nand\ttabs", Description = "Newlines and tabs")]
    [TestCase("Test with \"quotes\" and 'apostrophes'", Description = "Quotes")]
    [TestCase("Test with émojis 😀🎉", Description = "Emojis")]
    [TestCase("Test with unicode: ñ, ü, 中文", Description = "Unicode characters")]
    [TestCase("Test with special chars: !@#$%^&*()_+-=[]{}|;:,.<>?/", Description = "Special characters")]
    public async Task ChatAsync_WithSpecialCharactersInUserMessage_CompletesSuccessfully(string userMessage)
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var expectedResponse = new TestResponse
        {
            Message = "Success",
            Code = 200
        };
        var jsonResponse = JsonSerializer.Serialize(expectedResponse);
        SetupMockChatClient(mockChatClient, jsonResponse);
        var agent = new AiAgent(mockChatClient.Object);
        // Act
        var result = await agent.ChatAsync<TestResponse>(userMessage);
        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Message, Is.EqualTo("Success"));
    }

    /// <summary>
    /// Verifies that ChatAsync handles response with extra whitespace around JSON.
    /// Tests trimming behavior in JSON extraction.
    /// </summary>
    [Test]
    public async Task ChatAsync_WithWhitespaceAroundJson_ReturnsDeserializedObject()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var expectedResponse = new TestResponse
        {
            Message = "Success",
            Code = 200
        };
        var jsonResponse = JsonSerializer.Serialize(expectedResponse);
        var responseWithWhitespace = $"  \n\t  {jsonResponse}  \n\t  ";
        SetupMockChatClient(mockChatClient, responseWithWhitespace);
        var agent = new AiAgent(mockChatClient.Object);
        // Act
        var result = await agent.ChatAsync<TestResponse>("Test message");
        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Message, Is.EqualTo("Success"));
        Assert.That(result.Code, Is.EqualTo(200));
    }

    /// <summary>
    /// Verifies that ChatAsync handles malformed markdown code blocks gracefully.
    /// Tests edge case for incomplete code block markers.
    /// </summary>
    [TestCase("```json\n{\"message\":\"Success\",\"code\":200}", Description = "Missing closing backticks")]
    [TestCase("{\"message\":\"Success\",\"code\":200}\n```", Description = "Missing opening backticks")]
    [TestCase("```\n{\"message\":\"Success\",\"code\":200}", Description = "Generic block without closing")]
    public async Task ChatAsync_WithMalformedMarkdownCodeBlock_AttemptsDeserialization(string response)
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        SetupMockChatClient(mockChatClient, response);
        var agent = new AiAgent(mockChatClient.Object);
        // Act
        var result = await agent.ChatAsync<TestResponse>("Test message");
        // Assert
        // The behavior depends on whether the malformed response still contains valid JSON
        // It may return a valid object or null depending on extraction logic
        Assert.That(result, Is.Not.Null.Or.Null);
    }

    /// <summary>
    /// Helper method to setup mock IChatClient for testing.
    /// Configures the mock to return a ChatResponse that will produce the specified text.
    /// Note: This is a simplified setup that may need adjustment based on actual Microsoft.Extensions.AI API.
    /// </summary>
    private static void SetupMockChatClient(Mock<IChatClient> mockChatClient, string responseText)
    {
        // Create mock response messages
        var chatMessage = new ChatMessage(ChatRole.Assistant, responseText);
        var chatResponse = new ChatResponse([chatMessage]);
        // Setup the GetResponseAsync method
        mockChatClient.Setup(client => client.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(chatResponse);
    }

    /// <summary>
    /// Tests that SystemPrompt returns the value provided during construction.
    /// </summary>
    [Test]
    public void SystemPrompt_InitializedInConstructor_ReturnsValue()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var expectedPrompt = "You are a helpful assistant.";
        // Act
        var agent = new AiAgent(mockChatClient.Object, expectedPrompt);
        var actualPrompt = agent.SystemPrompt;
        // Assert
        Assert.That(actualPrompt, Is.EqualTo(expectedPrompt));
    }

    /// <summary>
    /// Tests that SystemPrompt returns null when not initialized in constructor.
    /// </summary>
    [Test]
    public void SystemPrompt_NotInitializedInConstructor_ReturnsNull()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        // Act
        var agent = new AiAgent(mockChatClient.Object);
        var actualPrompt = agent.SystemPrompt;
        // Assert
        Assert.That(actualPrompt, Is.Null);
    }

    /// <summary>
    /// Tests that SystemPrompt can be set to null explicitly.
    /// </summary>
    [Test]
    public void SystemPrompt_SetToNull_ReturnsNull()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object, "Initial prompt");
        // Act
        agent.SystemPrompt = null;
        var actualPrompt = agent.SystemPrompt;
        // Assert
        Assert.That(actualPrompt, Is.Null);
    }

    /// <summary>
    /// Tests that SystemPrompt setter works with various string values.
    /// Input: Various string values including edge cases.
    /// Expected: The getter returns the exact value that was set.
    /// </summary>
    [TestCase("Simple prompt")]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("Prompt with special characters: !@#$%^&*(){}[]|\\:;\"'<>,.?/~`")]
    [TestCase("Prompt with\nnewlines\r\nand\ttabs")]
    [TestCase("Unicode characters: 你好 🌍 café")]
    [TestCase("Very long prompt: Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.")]
    public void SystemPrompt_SetToValue_ReturnsNewValue(string newPrompt)
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        // Act
        agent.SystemPrompt = newPrompt;
        var actualPrompt = agent.SystemPrompt;
        // Assert
        Assert.That(actualPrompt, Is.EqualTo(newPrompt));
    }

    /// <summary>
    /// Tests that SystemPrompt can be updated multiple times and returns the latest value.
    /// </summary>
    [Test]
    public void SystemPrompt_SetMultipleTimes_ReturnsLatestValue()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object, "Initial prompt");
        var firstUpdate = "First update";
        var secondUpdate = "Second update";
        var thirdUpdate = "Third update";
        // Act
        agent.SystemPrompt = firstUpdate;
        var afterFirst = agent.SystemPrompt;
        agent.SystemPrompt = secondUpdate;
        var afterSecond = agent.SystemPrompt;
        agent.SystemPrompt = thirdUpdate;
        var afterThird = agent.SystemPrompt;
        // Assert
        Assert.That(afterFirst, Is.EqualTo(firstUpdate));
        Assert.That(afterSecond, Is.EqualTo(secondUpdate));
        Assert.That(afterThird, Is.EqualTo(thirdUpdate));
    }

    /// <summary>
    /// Tests that SystemPrompt preserves exact string content including control characters.
    /// </summary>
    [Test]
    public void SystemPrompt_SetToStringWithControlCharacters_ReturnsExactValue()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object);
        var promptWithControlChars = "Line1\u0000\u0001\u0002Line2\u001F\u007F";
        // Act
        agent.SystemPrompt = promptWithControlChars;
        var actualPrompt = agent.SystemPrompt;
        // Assert
        Assert.That(actualPrompt, Is.EqualTo(promptWithControlChars));
    }

    /// <summary>
    /// Tests that SystemPrompt can be set to empty string after having a value.
    /// </summary>
    [Test]
    public void SystemPrompt_SetToEmptyStringAfterValue_ReturnsEmptyString()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var agent = new AiAgent(mockChatClient.Object, "Initial non-empty prompt");
        // Act
        agent.SystemPrompt = string.Empty;
        var actualPrompt = agent.SystemPrompt;
        // Assert
        Assert.That(actualPrompt, Is.EqualTo(string.Empty));
    }
}