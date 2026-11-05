using Microsoft.Extensions.AI;
using Moq;
using ReceptyOks.Interfaces;
using ReceptyOks.Services.AgentTools;
using ReceptyOks.Shared.AI;

namespace ReceptyOks_UnitTests.Services;

/// <summary>
/// Unit tests for the <see cref="AgentToolsRegistrar"/> composite.
/// </summary>
[TestFixture]
public sealed class AgentToolsRegistrarTests
{
    private static AiAgent CreateAgent() => new(new Mock<IChatClient>().Object);

    private static Mock<IAgentToolProvider> CreateProvider(AgentToolGroup group)
    {
        var mock = new Mock<IAgentToolProvider>();
        mock.SetupGet(p => p.Group).Returns(group);
        return mock;
    }

    [Test]
    public void Constructor_NullProviders_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new AgentToolsRegistrar(null!));
        Assert.That(exception!.ParamName, Is.EqualTo("providers"));
    }

    [Test]
    public void RegisterTools_NullAgent_ThrowsArgumentNullException()
    {
        var provider = CreateProvider(AgentToolGroup.Recipes);
        var registrar = new AgentToolsRegistrar([provider.Object]);

        var exception = Assert.Throws<ArgumentNullException>(() => registrar.RegisterTools(null!));

        Assert.That(exception!.ParamName, Is.EqualTo("agent"));
        provider.Verify(p => p.Register(It.IsAny<AiAgent>()), Times.Never);
    }

    [Test]
    public void RegisterTools_NoGroups_RegistersAllProviders()
    {
        var providers = Enum.GetValues<AgentToolGroup>().Select(CreateProvider).ToList();
        var registrar = new AgentToolsRegistrar(providers.Select(p => p.Object));
        var agent = CreateAgent();

        registrar.RegisterTools(agent);

        foreach (var provider in providers)
        {
            provider.Verify(p => p.Register(agent), Times.Once);
        }
    }

    [Test]
    public void RegisterTools_SingleGroup_RegistersOnlyMatchingProviders()
    {
        var recipes = CreateProvider(AgentToolGroup.Recipes);
        var shopping = CreateProvider(AgentToolGroup.ShoppingList);
        var homeSeeker = CreateProvider(AgentToolGroup.HomeSeeker);
        var registrar = new AgentToolsRegistrar([recipes.Object, shopping.Object, homeSeeker.Object]);
        var agent = CreateAgent();

        registrar.RegisterTools(agent, AgentToolGroup.ShoppingList);

        shopping.Verify(p => p.Register(agent), Times.Once);
        recipes.Verify(p => p.Register(It.IsAny<AiAgent>()), Times.Never);
        homeSeeker.Verify(p => p.Register(It.IsAny<AiAgent>()), Times.Never);
    }

    [Test]
    public void RegisterTools_MultipleGroups_RegistersAllMatchingProviders()
    {
        var recipes = CreateProvider(AgentToolGroup.Recipes);
        var shopping = CreateProvider(AgentToolGroup.ShoppingList);
        var homeSeeker = CreateProvider(AgentToolGroup.HomeSeeker);
        var web = CreateProvider(AgentToolGroup.Web);
        var registrar = new AgentToolsRegistrar([recipes.Object, shopping.Object, homeSeeker.Object, web.Object]);
        var agent = CreateAgent();

        registrar.RegisterTools(agent, AgentToolGroup.Recipes, AgentToolGroup.HomeSeeker, AgentToolGroup.Web);

        recipes.Verify(p => p.Register(agent), Times.Once);
        homeSeeker.Verify(p => p.Register(agent), Times.Once);
        web.Verify(p => p.Register(agent), Times.Once);
        shopping.Verify(p => p.Register(It.IsAny<AiAgent>()), Times.Never);
    }

    [Test]
    public void RegisterTools_DuplicateGroups_RegistersProviderOnce()
    {
        var recipes = CreateProvider(AgentToolGroup.Recipes);
        var registrar = new AgentToolsRegistrar([recipes.Object]);
        var agent = CreateAgent();

        registrar.RegisterTools(agent, AgentToolGroup.Recipes, AgentToolGroup.Recipes);

        recipes.Verify(p => p.Register(agent), Times.Once);
    }

    [Test]
    public void RegisterTools_MultipleProvidersInSameGroup_RegistersAllOfThem()
    {
        var first = CreateProvider(AgentToolGroup.Recipes);
        var second = CreateProvider(AgentToolGroup.Recipes);
        var registrar = new AgentToolsRegistrar([first.Object, second.Object]);
        var agent = CreateAgent();

        registrar.RegisterTools(agent, AgentToolGroup.Recipes);

        first.Verify(p => p.Register(agent), Times.Once);
        second.Verify(p => p.Register(agent), Times.Once);
    }

    [Test]
    public void RegisterTools_GroupWithoutProvider_RegistersNothing()
    {
        var recipes = CreateProvider(AgentToolGroup.Recipes);
        var registrar = new AgentToolsRegistrar([recipes.Object]);

        Assert.DoesNotThrow(() => registrar.RegisterTools(CreateAgent(), AgentToolGroup.Web));

        recipes.Verify(p => p.Register(It.IsAny<AiAgent>()), Times.Never);
    }

    [Test]
    public void RegisterTools_NoProviders_DoesNotThrow()
    {
        var registrar = new AgentToolsRegistrar([]);

        Assert.DoesNotThrow(() => registrar.RegisterTools(CreateAgent()));
    }

    [Test]
    public void RegisterTools_PreservesProviderOrder()
    {
        var order = new List<string>();
        var first = CreateProvider(AgentToolGroup.HomeSeeker);
        var second = CreateProvider(AgentToolGroup.Recipes);
        first.Setup(p => p.Register(It.IsAny<AiAgent>())).Callback(() => order.Add("first"));
        second.Setup(p => p.Register(It.IsAny<AiAgent>())).Callback(() => order.Add("second"));
        var registrar = new AgentToolsRegistrar([first.Object, second.Object]);

        registrar.RegisterTools(CreateAgent());

        Assert.That(order, Is.EqualTo(new[] { "first", "second" }));
    }

    [Test]
    public void RegisterTools_ProvidersEnumeratedOnceAtConstruction()
    {
        var enumerations = 0;
        IEnumerable<IAgentToolProvider> Source()
        {
            enumerations++;
            yield return CreateProvider(AgentToolGroup.Recipes).Object;
        }

        var registrar = new AgentToolsRegistrar(Source());
        registrar.RegisterTools(CreateAgent());
        registrar.RegisterTools(CreateAgent());

        Assert.That(enumerations, Is.EqualTo(1));
    }

    [Test]
    public void RegisterTools_ProviderThrows_PropagatesException()
    {
        var failing = CreateProvider(AgentToolGroup.Recipes);
        failing.Setup(p => p.Register(It.IsAny<AiAgent>())).Throws(new InvalidOperationException("boom"));
        var registrar = new AgentToolsRegistrar([failing.Object]);

        Assert.Throws<InvalidOperationException>(() => registrar.RegisterTools(CreateAgent()));
    }
}
