using System.Text.Json.Serialization;

namespace PowerMeterBridge;

[JsonSerializable(typeof(BridgeState))]
[JsonSourceGenerationOptions(
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
internal partial class BridgeStateJsonContext : JsonSerializerContext
{
}
