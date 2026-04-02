using ReceptyOks.BlazorComponents.Services;

namespace ReceptyOks_UnitTests.Services;

[TestFixture]
public class MemoryChartStateTests
{
    private MemoryChartState _chartState = null!;

    [SetUp]
    public void SetUp()
    {
        _chartState = new MemoryChartState();
    }

    #region Initial State Tests

    [Test]
    public void Constructor_InitialState_MemoryDataIsEmpty()
    {
        // Assert
        Assert.That(_chartState.MemoryData, Is.Empty);
    }

    [Test]
    public void Constructor_InitialState_IsLoadingIsFalse()
    {
        // Assert
        Assert.That(_chartState.IsLoading, Is.False);
    }

    [Test]
    public void Constructor_InitialState_ThresholdMBIsDefault()
    {
        // Assert
        Assert.That(_chartState.ThresholdMB, Is.EqualTo(400));
    }

    #endregion

    #region SetLoading Tests

    [Test]
    public void SetLoading_True_SetsIsLoadingToTrue()
    {
        // Act
        _chartState.SetLoading(true);

        // Assert
        Assert.That(_chartState.IsLoading, Is.True);
    }

    [Test]
    public void SetLoading_False_SetsIsLoadingToFalse()
    {
        // Arrange
        _chartState.SetLoading(true);

        // Act
        _chartState.SetLoading(false);

        // Assert
        Assert.That(_chartState.IsLoading, Is.False);
    }

    [Test]
    public void SetLoading_RaisesOnChangeEvent()
    {
        // Arrange
        var eventRaised = false;
        _chartState.OnChange += () => eventRaised = true;

        // Act
        _chartState.SetLoading(true);

        // Assert
        Assert.That(eventRaised, Is.True);
    }

    #endregion

    #region AddDataPoint Tests

    [Test]
    public void AddDataPoint_AddsPointToMemoryData()
    {
        // Arrange
        var timestamp = DateTime.Now;
        var memoryMB = 150m;

        // Act
        _chartState.AddDataPoint(timestamp, memoryMB);

        // Assert
        Assert.That(_chartState.MemoryData, Has.Count.EqualTo(1));
        Assert.That(_chartState.MemoryData[0].Timestamp, Is.EqualTo(timestamp));
        Assert.That(_chartState.MemoryData[0].MemoryMB, Is.EqualTo(memoryMB));
    }

    [Test]
    public void AddDataPoint_MultiplePoints_AddsAllPoints()
    {
        // Act
        _chartState.AddDataPoint(DateTime.Now, 100m);
        _chartState.AddDataPoint(DateTime.Now.AddSeconds(1), 150m);
        _chartState.AddDataPoint(DateTime.Now.AddSeconds(2), 200m);

        // Assert
        Assert.That(_chartState.MemoryData, Has.Count.EqualTo(3));
    }

    [Test]
    public void AddDataPoint_MoreThan60Points_KeepsOnly60()
    {
        // Arrange & Act
        for (int i = 0; i < 65; i++)
        {
            _chartState.AddDataPoint(DateTime.Now.AddSeconds(i), i * 10m);
        }

        // Assert
        Assert.That(_chartState.MemoryData, Has.Count.EqualTo(60));
    }

    [Test]
    public void AddDataPoint_MoreThan60Points_RemovesOldestFirst()
    {
        // Arrange & Act
        for (int i = 0; i < 65; i++)
        {
            _chartState.AddDataPoint(DateTime.Now.AddSeconds(i), i * 10m);
        }

        // Assert - first 5 points (0-4) should be removed, so first remaining is 50 (index 5 * 10)
        Assert.That(_chartState.MemoryData[0].MemoryMB, Is.EqualTo(50m));
    }

    [Test]
    public void AddDataPoint_RaisesOnChangeEvent()
    {
        // Arrange
        var eventRaised = false;
        _chartState.OnChange += () => eventRaised = true;

        // Act
        _chartState.AddDataPoint(DateTime.Now, 100m);

        // Assert
        Assert.That(eventRaised, Is.True);
    }

    [Test]
    public void AddDataPoint_MultiplePoints_RaisesOnChangeEventForEach()
    {
        // Arrange
        var eventCount = 0;
        _chartState.OnChange += () => eventCount++;

        // Act
        _chartState.AddDataPoint(DateTime.Now, 100m);
        _chartState.AddDataPoint(DateTime.Now, 200m);
        _chartState.AddDataPoint(DateTime.Now, 300m);

        // Assert
        Assert.That(eventCount, Is.EqualTo(3));
    }

    #endregion

    #region SetData Tests

    [Test]
    public void SetData_EmptyCollection_ClearsMemoryData()
    {
        // Arrange
        _chartState.AddDataPoint(DateTime.Now, 100m);

        // Act
        _chartState.SetData([]);

        // Assert
        Assert.That(_chartState.MemoryData, Is.Empty);
    }

    [Test]
    public void SetData_WithData_ReplacesExistingData()
    {
        // Arrange
        _chartState.AddDataPoint(DateTime.Now, 100m);
        var newData = new List<MemoryChartState.MemoryDataPoint>
        {
            new() { Timestamp = DateTime.Now, MemoryMB = 200m },
            new() { Timestamp = DateTime.Now.AddSeconds(1), MemoryMB = 250m }
        };

        // Act
        _chartState.SetData(newData);

        // Assert
        Assert.That(_chartState.MemoryData, Has.Count.EqualTo(2));
        Assert.That(_chartState.MemoryData[0].MemoryMB, Is.EqualTo(200m));
        Assert.That(_chartState.MemoryData[1].MemoryMB, Is.EqualTo(250m));
    }

    [Test]
    public void SetData_RaisesOnChangeEvent()
    {
        // Arrange
        var eventRaised = false;
        _chartState.OnChange += () => eventRaised = true;

        // Act
        _chartState.SetData([new() { MemoryMB = 100m }]);

        // Assert
        Assert.That(eventRaised, Is.True);
    }

    [Test]
    public void SetData_WithIEnumerable_AddsAllItems()
    {
        // Arrange
        IEnumerable<MemoryChartState.MemoryDataPoint> GetData()
        {
            yield return new() { Timestamp = DateTime.Now, MemoryMB = 100m };
            yield return new() { Timestamp = DateTime.Now.AddSeconds(1), MemoryMB = 200m };
        }

        // Act
        _chartState.SetData(GetData());

        // Assert
        Assert.That(_chartState.MemoryData, Has.Count.EqualTo(2));
    }

    #endregion

    #region Clear Tests

    [Test]
    public void Clear_RemovesAllDataPoints()
    {
        // Arrange
        _chartState.AddDataPoint(DateTime.Now, 100m);
        _chartState.AddDataPoint(DateTime.Now.AddSeconds(1), 200m);

        // Act
        _chartState.Clear();

        // Assert
        Assert.That(_chartState.MemoryData, Is.Empty);
    }

    [Test]
    public void Clear_RaisesOnChangeEvent()
    {
        // Arrange
        _chartState.AddDataPoint(DateTime.Now, 100m);
        var eventRaised = false;
        _chartState.OnChange += () => eventRaised = true;

        // Act
        _chartState.Clear();

        // Assert
        Assert.That(eventRaised, Is.True);
    }

    [Test]
    public void Clear_WhenAlreadyEmpty_DoesNotThrow()
    {
        // Act & Assert
        Assert.DoesNotThrow(() => _chartState.Clear());
    }

    #endregion

    #region ThresholdMB Tests

    [Test]
    public void ThresholdMB_CanBeSet()
    {
        // Act
        _chartState.ThresholdMB = 512;

        // Assert
        Assert.That(_chartState.ThresholdMB, Is.EqualTo(512));
    }

    [Test]
    public void ThresholdMB_CanBeSetToZero()
    {
        // Act
        _chartState.ThresholdMB = 0;

        // Assert
        Assert.That(_chartState.ThresholdMB, Is.EqualTo(0));
    }

    #endregion

    #region MemoryDataPoint Tests

    [Test]
    public void MemoryDataPoint_DefaultTimestamp_IsNow()
    {
        // Arrange
        var before = DateTime.Now;
        var dataPoint = new MemoryChartState.MemoryDataPoint();
        var after = DateTime.Now;

        // Assert
        Assert.That(dataPoint.Timestamp, Is.InRange(before, after));
    }

    [Test]
    public void MemoryDataPoint_DefaultMemoryMB_IsZero()
    {
        // Arrange
        var dataPoint = new MemoryChartState.MemoryDataPoint();

        // Assert
        Assert.That(dataPoint.MemoryMB, Is.EqualTo(0));
    }

    [Test]
    public void MemoryDataPoint_CanSetProperties()
    {
        // Arrange
        var timestamp = new DateTime(2024, 1, 15, 10, 30, 0);
        var memoryMB = 256.5m;

        // Act
        var dataPoint = new MemoryChartState.MemoryDataPoint
        {
            Timestamp = timestamp,
            MemoryMB = memoryMB
        };

        // Assert
        Assert.That(dataPoint.Timestamp, Is.EqualTo(timestamp));
        Assert.That(dataPoint.MemoryMB, Is.EqualTo(memoryMB));
    }

    #endregion

    #region OnChange Event Tests

    [Test]
    public void OnChange_NoSubscribers_DoesNotThrow()
    {
        // Act & Assert
        Assert.DoesNotThrow(() => _chartState.SetLoading(true));
        Assert.DoesNotThrow(() => _chartState.AddDataPoint(DateTime.Now, 100m));
        Assert.DoesNotThrow(() => _chartState.Clear());
    }

    [Test]
    public void OnChange_MultipleSubscribers_AllAreNotified()
    {
        // Arrange
        var subscriber1Called = false;
        var subscriber2Called = false;
        _chartState.OnChange += () => subscriber1Called = true;
        _chartState.OnChange += () => subscriber2Called = true;

        // Act
        _chartState.SetLoading(true);

        // Assert
        Assert.That(subscriber1Called, Is.True);
        Assert.That(subscriber2Called, Is.True);
    }

    [Test]
    public void OnChange_AfterUnsubscribe_NotCalled()
    {
        // Arrange
        var eventRaised = false;
        void Handler() => eventRaised = true;
        _chartState.OnChange += Handler;
        _chartState.OnChange -= Handler;

        // Act
        _chartState.SetLoading(true);

        // Assert
        Assert.That(eventRaised, Is.False);
    }

    #endregion

    #region MemoryData Immutability Tests

    [Test]
    public void MemoryData_ReturnsReadOnlyCollection()
    {
        // Arrange
        _chartState.AddDataPoint(DateTime.Now, 100m);

        // Assert
        Assert.That(_chartState.MemoryData, Is.InstanceOf<IReadOnlyList<MemoryChartState.MemoryDataPoint>>());
    }

    [Test]
    public void MemoryData_CannotBeModifiedDirectly()
    {
        // Arrange
        _chartState.AddDataPoint(DateTime.Now, 100m);
        var memoryData = _chartState.MemoryData;

        // Assert - IReadOnlyList doesn't have Add method, so this verifies immutability
        Assert.That(memoryData, Is.Not.InstanceOf<List<MemoryChartState.MemoryDataPoint>>());
    }

    #endregion
}
