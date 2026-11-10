namespace PowerMeterBridge;

/// <summary>
/// CNYIoT 电表客户端工厂
/// </summary>
public sealed class CnyiotMeterClientFactory : IMeterClientFactory
{
    public IMeterClient CreateClient(DeviceConfig config, HttpClient httpClient)
    {
        var sourceUrl = config.GetString("sourceUrl");
        if (string.IsNullOrWhiteSpace(sourceUrl))
        {
            throw new ArgumentException(
                $"设备 '{config.Id}': 配置中必须包含 'sourceUrl' 参数",
                nameof(config));
        }

        if (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out _))
        {
            throw new ArgumentException(
                $"设备 '{config.Id}': sourceUrl 不是有效的 URL",
                nameof(config));
        }

        return new CnyiotMeterClient(httpClient, sourceUrl);
    }
}
