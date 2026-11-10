using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace PowerMeterBridge;

/// <summary>
/// 单个设备的配置
/// </summary>
public sealed class DeviceConfig
{
    /// <summary>设备唯一标识</summary>
    public required string Id { get; set; }

    /// <summary>设备名称（用于日志和 UI 显示）</summary>
    public required string Name { get; set; }

    /// <summary>品牌标识（如 "cnyiot", "state_grid"）</summary>
    public required string Brand { get; set; }

    /// <summary>设备类型（如 "prepaid_meter", "smart_meter"）</summary>
    public required string Type { get; set; }

    /// <summary>是否启用此设备</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 品牌特定的配置
    /// 不同品牌使用不同的配置参数
    /// </summary>
    public Dictionary<string, JsonElement> Config { get; set; } = [];

    /// <summary>Home Assistant 集成配置</summary>
    public required HomeAssistantDeviceOptions Ha { get; set; }

    /// <summary>HTTP 请求配置（可选，用于网络请求类型的设备）</summary>
    public HttpOptions Http { get; set; } = new();

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id))
            throw new InvalidOperationException("Device.Id 不能为空");

        if (string.IsNullOrWhiteSpace(Brand))
            throw new InvalidOperationException($"设备 '{Id}': Brand 不能为空");

        if (string.IsNullOrWhiteSpace(Type))
            throw new InvalidOperationException($"设备 '{Id}': Type 不能为空");

        // 禁用设备无需验证其他字段
        if (!Enabled)
            return;

        Ha?.Validate(Id);
    }

    /// <summary>获取配置中的字符串值</summary>
    public string GetString(string key) =>
        Config.TryGetValue(key, out var val)
            ? val.GetString() ?? ""
            : "";

    /// <summary>获取配置中的字符串值，带默认值</summary>
    public string GetString(string key, string defaultValue) =>
        Config.TryGetValue(key, out var val)
            ? val.GetString() ?? defaultValue
            : defaultValue;

    /// <summary>获取配置中的整数值</summary>
    public int GetInt(string key, int defaultValue = 0)
    {
        if (Config.TryGetValue(key, out var val) && val.TryGetInt32(out var intVal))
            return intVal;
        return defaultValue;
    }

    /// <summary>获取配置中的布尔值</summary>
    public bool GetBool(string key, bool defaultValue = false)
    {
        if (Config.TryGetValue(key, out var val) && val.ValueKind == JsonValueKind.True)
            return true;
        if (val.ValueKind == JsonValueKind.False)
            return false;
        return defaultValue;
    }

    /// <summary>获取配置中的对象值</summary>
    public T GetObject<T>(string key, JsonTypeInfo<T> typeInfo)
    {
        if (!Config.TryGetValue(key, out var val))
            throw new KeyNotFoundException($"配置项 '{key}' 不存在");

        return JsonSerializer.Deserialize(val, typeInfo)
            ?? throw new InvalidOperationException($"反序列化 '{key}' 失败");
    }
}

/// <summary>
/// Home Assistant 设备集成配置
/// </summary>
public sealed class HomeAssistantDeviceOptions
{
    /// <summary>设备 ID（用于 MQTT discovery）</summary>
    public required string DeviceId { get; set; }

    /// <summary>设备名称（用户可见）</summary>
    public required string FriendlyName { get; set; }

    /// <summary>MQTT 主题前缀</summary>
    public required string BaseTopic { get; set; }

    /// <summary>设备制造商</summary>
    public string Manufacturer { get; set; } = "";

    /// <summary>设备型号</summary>
    public string Model { get; set; } = "";

    public void Validate(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(DeviceId))
            throw new InvalidOperationException($"设备 '{deviceId}': Ha.DeviceId 不能为空");

        if (string.IsNullOrWhiteSpace(FriendlyName))
            throw new InvalidOperationException($"设备 '{deviceId}': Ha.FriendlyName 不能为空");

        if (string.IsNullOrWhiteSpace(BaseTopic))
            throw new InvalidOperationException($"设备 '{deviceId}': Ha.BaseTopic 不能为空");
    }
}
