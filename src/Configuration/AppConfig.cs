using System.Text.Json;

namespace PowerMeterBridge;

public sealed class AppConfig
{
    public string SourceUrl { get; set; } = "http://www.wap.cnyiot.com/nat/pay.aspx?mid=19000751693";
    public int FailureRetrySeconds { get; set; } = 60;
    public HttpOptions Http { get; set; } = new();
    public MqttOptions Mqtt { get; set; } = new();
    public HomeAssistantOptions HomeAssistant { get; set; } = new();
    public StorageOptions Storage { get; set; } = new();

    public void Validate()
    {
        if (!Uri.TryCreate(SourceUrl, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException("SourceUrl 不是合法的绝对地址。");
        }

        if (FailureRetrySeconds <= 0)
        {
            throw new InvalidOperationException("FailureRetrySeconds 必须大于 0。");
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
    public string DiscoveryPrefix { get; set; } = "homeassistant";
    public string DeviceId { get; set; } = "cnyiot_meter_19000751693";
    public string FriendlyName { get; set; } = "辰域智控 19000751693";
    public string BaseTopic { get; set; } = "home/cnyiot/19000751693";
    public string Currency { get; set; } = "CNY";
    public string Manufacturer { get; set; } = "CNYIoT";
    public string Model { get; set; } = "Prepaid Electric Meter";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(DiscoveryPrefix))
        {
            throw new InvalidOperationException("HomeAssistant.DiscoveryPrefix 不能为空。");
        }

        if (string.IsNullOrWhiteSpace(DeviceId))
        {
            throw new InvalidOperationException("HomeAssistant.DeviceId 不能为空。");
        }

        if (string.IsNullOrWhiteSpace(FriendlyName))
        {
            throw new InvalidOperationException("HomeAssistant.FriendlyName 不能为空。");
        }

        if (string.IsNullOrWhiteSpace(BaseTopic))
        {
            throw new InvalidOperationException("HomeAssistant.BaseTopic 不能为空。");
        }

        if (string.IsNullOrWhiteSpace(Currency))
        {
            throw new InvalidOperationException("HomeAssistant.Currency 不能为空。");
        }
    }
}

public sealed class StorageOptions
{
    public string StateFilePath { get; set; } = "data/bridge-state.json";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(StateFilePath))
        {
            throw new InvalidOperationException("Storage.StateFilePath 不能为空。");
        }
    }
}

public readonly record struct LoadedAppConfig(AppConfig Config, string ConfigPath, string ConfigDirectory);

public static class AppConfigLoader
{
    public static LoadedAppConfig Load(string[] args)
    {
        var candidate = args.Length > 1 ? args[1] : "appsettings.json";
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
