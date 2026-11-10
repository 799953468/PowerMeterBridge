using FluentAssertions;

namespace PowerMeterBridge.Tests;

public class BridgeStateStoreTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _testFilePath;

    public BridgeStateStoreTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"PowerMeterBridge.Tests.{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);
        _testFilePath = Path.Combine(_tempDirectory, "test-state.json");
    }

    [Fact]
    public async Task LoadAsync_FileDoesNotExist_ShouldReturnDefaultState()
    {
        // Arrange
        var store = new BridgeStateStore(_testFilePath);

        // Act
        var state = await store.LoadAsync(CancellationToken.None);

        // Assert
        state.Should().NotBeNull();
        state.LastRemainingEnergyKwh.Should().BeNull();
        state.LastRemainingAmountCny.Should().BeNull();
        state.TotalConsumedEnergyKwh.Should().Be(0);
        state.TotalConsumedCostCny.Should().Be(0);
        state.CurrentPowerKw.Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_ShouldCreateDirectoryIfNotExists()
    {
        // Arrange
        var nestedPath = Path.Combine(_tempDirectory, "nested", "directory", "state.json");
        var store = new BridgeStateStore(nestedPath);
        var state = new BridgeState
        {
            LastRemainingEnergyKwh = 100.5m,
            LastRemainingAmountCny = 100.5m
        };

        // Act
        await store.SaveAsync(state, CancellationToken.None);

        // Assert
        File.Exists(nestedPath).Should().BeTrue();
    }

    [Fact]
    public async Task SaveAndLoad_ShouldPersistState()
    {
        // Arrange
        var store = new BridgeStateStore(_testFilePath);
        var originalState = new BridgeState
        {
            LastRemainingEnergyKwh = 125.45m,
            LastRemainingAmountCny = 125.45m,
            TotalConsumedEnergyKwh = 1523.8642m,
            TotalConsumedCostCny = 1523.86m,
            CurrentPowerKw = 2.450m,
            LastObservedAt = new DateTimeOffset(2026, 11, 10, 14, 30, 15, TimeSpan.FromHours(8))
        };

        // Act
        await store.SaveAsync(originalState, CancellationToken.None);
        var loadedState = await store.LoadAsync(CancellationToken.None);

        // Assert
        loadedState.Should().BeEquivalentTo(originalState);
    }

    [Fact]
    public async Task SaveAsync_MultipleTimes_ShouldOverwritePreviousState()
    {
        // Arrange
        var store = new BridgeStateStore(_testFilePath);
        var state1 = new BridgeState { LastRemainingEnergyKwh = 100m };
        var state2 = new BridgeState { LastRemainingEnergyKwh = 200m };

        // Act
        await store.SaveAsync(state1, CancellationToken.None);
        await store.SaveAsync(state2, CancellationToken.None);
        var loadedState = await store.LoadAsync(CancellationToken.None);

        // Assert
        loadedState.LastRemainingEnergyKwh.Should().Be(200m);
    }

    [Fact]
    public async Task LoadAsync_CorruptedFile_ShouldThrowException()
    {
        // Arrange
        await File.WriteAllTextAsync(_testFilePath, "{ invalid json }");
        var store = new BridgeStateStore(_testFilePath);

        // Act
        Func<Task> act = async () => await store.LoadAsync(CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task SaveAndLoad_WithDecimalPrecision_ShouldMaintainAccuracy()
    {
        // Arrange
        var store = new BridgeStateStore(_testFilePath);
        var originalState = new BridgeState
        {
            LastRemainingEnergyKwh = 123.456789m,
            TotalConsumedEnergyKwh = 1234567.89012345m,
            CurrentPowerKw = 2.34567m
        };

        // Act
        await store.SaveAsync(originalState, CancellationToken.None);
        var loadedState = await store.LoadAsync(CancellationToken.None);

        // Assert
        loadedState.LastRemainingEnergyKwh.Should().Be(originalState.LastRemainingEnergyKwh);
        loadedState.TotalConsumedEnergyKwh.Should().Be(originalState.TotalConsumedEnergyKwh);
        loadedState.CurrentPowerKw.Should().Be(originalState.CurrentPowerKw);
    }

    [Fact]
    public async Task SaveAsync_WithCancellationToken_ShouldRespectCancellation()
    {
        // Arrange
        var store = new BridgeStateStore(_testFilePath);
        var state = new BridgeState();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        Func<Task> act = async () => await store.SaveAsync(state, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}
