using System.Text.Json.Serialization;

namespace PowerMeterBridge;

[JsonSerializable(typeof(AppConfig))]
[JsonSerializable(typeof(DeviceConfig))]
[JsonSerializable(typeof(HomeAssistantDeviceOptions))]
[JsonSerializable(typeof(HttpOptions))]
[JsonSerializable(typeof(MqttOptions))]
[JsonSerializable(typeof(HomeAssistantOptions))]
[JsonSerializable(typeof(StorageOptions))]
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
internal partial class AppConfigJsonContext : JsonSerializerContext
{
}
