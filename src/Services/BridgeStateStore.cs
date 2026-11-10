using System.Text.Json;

namespace PowerMeterBridge;

public sealed class BridgeStateStore
{
    private readonly string _stateFilePath;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    public BridgeStateStore(string stateFilePath)
    {
        _stateFilePath = stateFilePath;
    }

    public async Task<BridgeState> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_stateFilePath))
        {
            return new BridgeState();
        }

        await using var stream = File.OpenRead(_stateFilePath);
        var state = await JsonSerializer.DeserializeAsync<BridgeState>(stream, _jsonOptions, cancellationToken);
        return state ?? new BridgeState();
    }

    public async Task SaveAsync(BridgeState state, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_stateFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(_stateFilePath);
        await JsonSerializer.SerializeAsync(stream, state, _jsonOptions, cancellationToken);
    }
}
