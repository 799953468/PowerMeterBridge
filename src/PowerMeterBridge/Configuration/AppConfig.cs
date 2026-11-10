using System.Text.Json;

namespace PowerMeterBridge;

/// <summary>
/// 应用全局配置
/// 支持多个设备的配置
/// </summary>
public sealed class AppConfig
{
    /// <summary>MQTT 连接配置</summary>
    public MqttOptions Mqtt { get; set; } = new();

    /// <summary>Home Assistant 集成配置</summary>
    public HomeAssistantOptions HomeAssistant { get; set; } = new();

    /// <summary>存储配置</summary>
    public StorageOptions Storage { get; set; } = new();

    /// <summary>轮询间隔（秒）</summary>
    public int PollIntervalSeconds { get; set; } = 60;

    /// <summary>失败重试间隔（秒）</summary>
    public int FailureRetrySeconds { get; set; } = 60;

    /// <summary>所有设备配置</summary>
    public List<DeviceConfig> Devices { get; set; } = [];

    public void Validate()
    {
        if (Devices.Count == 0)
        {
            throw new InvalidOperationException("至少需要配置一个设备。");
        }

        if (PollIntervalSeconds <= 0)
        {
            throw new InvalidOperationException("PollIntervalSeconds 必须大于 0。");
        }

        if (FailureRetrySeconds <= 0)
        {
            throw new InvalidOperationException("FailureRetrySeconds 必须大于 0。");
        }

        // 检查设备 ID 唯一性
        var deviceIds = new HashSet<string>();
        foreach (var device in Devices)
        {
            if (!deviceIds.Add(device.Id))
            {
                throw new InvalidOperationException($"重复的设备 ID：'{device.Id}'");
            }

            device.Validate();
        }

        Mqtt.Validate();
        HomeAssistant.Validate();
        Storage.Validate();
    }
}

public sealed class HttpOptions
{
    public int TimeoutSeconds { get; set; } = 20;
    public string UserAgent { get; set; } =
        "Mozilla/5.0 (compatible; PowerMeterBridge/1.0; +https://github.com/dotnet)";
}

public sealed class MqttOptions
{
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 1883;
    public string ClientId { get; set; } = "cnyiot-power-mqtt-bridge";
    public string? Username { get; set; }
    public string? Password { get; set; }
    public int KeepAliveSeconds { get; set; } = 60;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Host))
        {
            throw new InvalidOperationException("Mqtt.Host 不能为空。");
        }

        if (Port <= 0 || Port > 65535)
        {
            throw new InvalidOperationException("Mqtt.Port 必须在 1-65535 之间。");
        }

        if (string.IsNullOrWhiteSpace(ClientId))
        {
            throw new InvalidOperationException("Mqtt.ClientId 不能为空。");
        }

        if (KeepAliveSeconds <= 0)
        {
            throw new InvalidOperationException("Mqtt.KeepAliveSeconds 必须大于 0。");
        }
    }
}

public sealed class HomeAssistantOptions
{
    /// <summary>Home Assistant MQTT Discovery 前缀</summary>
    public string DiscoveryPrefix { get; set; } = "homeassistant";

    /// <summary>货币单位代码（如 CNY, USD）</summary>
    public string Currency { get; set; } = "CNY";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(DiscoveryPrefix))
        {
            throw new InvalidOperationException("HomeAssistant.DiscoveryPrefix 不能为空。");
        }

        if (string.IsNullOrWhiteSpace(Currency))
        {
            throw new InvalidOperationException("HomeAssistant.Currency 不能为空。");
        }
    }
}

public sealed class StorageOptions
{
    /// <summary>状态文件存储目录</summary>
    public string StateDirectory { get; set; } = "data/states";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(StateDirectory))
        {
            throw new InvalidOperationException("Storage.StateDirectory 不能为空。");
        }
    }
}

public readonly record struct LoadedAppConfig(AppConfig Config, string ConfigPath, string ConfigDirectory);

public static class AppConfigLoader
{
    public static LoadedAppConfig Load(string[] args)
    {
        var candidate = args.Length > 0 ? args[0] : "appsettings.json";
        var currentDirectoryPath = Path.GetFullPath(candidate, Directory.GetCurrentDirectory());
        var baseDirectoryPath = Path.GetFullPath(candidate, AppContext.BaseDirectory);

        var resolvedPath = File.Exists(currentDirectoryPath)
            ? currentDirectoryPath
            : File.Exists(baseDirectoryPath)
                ? baseDirectoryPath
                : currentDirectoryPath;

        if (!File.Exists(resolvedPath))
        {
            throw new FileNotFoundException(
                $"找不到配置文件：{resolvedPath}。请先复制 appsettings.json.example 为 appsettings.json。");
        }

        var json = File.ReadAllText(resolvedPath);
        var config = JsonSerializer.Deserialize(json, AppConfigJsonContext.Default.AppConfig)
                     ?? throw new InvalidOperationException("配置文件解析失败。");

        config.Validate();

        return new LoadedAppConfig(
            config,
            resolvedPath,
            Path.GetDirectoryName(resolvedPath) ?? Directory.GetCurrentDirectory());
    }
}
