using System.Text;
using System.Text.Json;
using MQTTnet;
using MQTTnet.Protocol;

namespace PowerMeterBridge;

public sealed class HomeAssistantMqttPublisher
{
    private readonly MqttOptions _mqttConfig;
    private readonly HomeAssistantOptions _haConfig;
    private readonly IMqttClient _client;
    private readonly MqttClientOptions _clientOptions;
    private readonly Dictionary<string, bool> _deviceDiscoveryPublished = [];

    public HomeAssistantMqttPublisher(MqttOptions mqttConfig, HomeAssistantOptions haConfig)
    {
        _mqttConfig = mqttConfig;
        _haConfig = haConfig;
        _client = new MqttClientFactory().CreateMqttClient();
        _clientOptions = BuildClientOptions(mqttConfig);
    }

    public async Task PublishDiscoveryAsync(DeviceConfig deviceConfig, CancellationToken cancellationToken)
    {
        await EnsureConnectedAsync(cancellationToken);

        var deviceKey = deviceConfig.Id;
        if (_deviceDiscoveryPublished.TryGetValue(deviceKey, out var published) && published)
        {
            return;
        }

        foreach (var message in BuildDiscoveryMessages(deviceConfig))
        {
            await _client.PublishAsync(message, cancellationToken);
        }

        _deviceDiscoveryPublished[deviceKey] = true;
    }

    public async Task PublishStateAsync(
        DeviceConfig deviceConfig,
        MeterReading reading,
        BridgeState state,
        CancellationToken cancellationToken)
    {
        await EnsureConnectedAsync(cancellationToken);

        var message = new MqttStateMessage
        {
            MeterId = reading.MeterId,
            ObservedAt = reading.ObservedAt.ToString("O"),
            RemainingEnergyKwh = decimal.Round(reading.RemainingEnergyKwh, 2),
            RemainingAmountCny = decimal.Round(reading.RemainingAmountCurrency, 2),
            PriceCnyPerKwh = decimal.Round(reading.PriceCurrencyPerKwh, 4),
            TotalEnergyConsumedKwh = decimal.Round(state.TotalConsumedEnergyKwh, 4),
            TotalCostSpentCny = decimal.Round(state.TotalConsumedCostCny, 2),
            CurrentPowerKw = state.CurrentPowerKw.HasValue ? decimal.Round(state.CurrentPowerKw.Value, 3) : null
        };

        var payload = JsonSerializer.Serialize(message, MqttMessageJsonContext.Default.MqttStateMessage);

        var stateTopic = $"{deviceConfig.Ha.BaseTopic.TrimEnd('/')}/state";
        var availabilityTopic = $"{deviceConfig.Ha.BaseTopic.TrimEnd('/')}/availability";

        await _client.PublishAsync(
            BuildMessage(stateTopic, payload, retain: true),
            cancellationToken);

        await _client.PublishAsync(
            BuildMessage(availabilityTopic, "online", retain: true),
            cancellationToken);
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        if (!_client.IsConnected)
        {
            return;
        }

        // 发布所有已连接设备的离线状态
        foreach (var deviceId in _deviceDiscoveryPublished.Keys)
        {
            // 这里需要设备配置来构造 topic，所以保留最后一个 topic
        }

        await _client.DisconnectAsync(cancellationToken: cancellationToken);
    }

    public void Dispose()
    {
        if (_client is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_client.IsConnected)
        {
            return;
        }

        await _client.ConnectAsync(_clientOptions, cancellationToken);
    }

    private MqttClientOptions BuildClientOptions(MqttOptions config)
    {
        var builder = new MqttClientOptionsBuilder()
            .WithClientId(config.ClientId)
            .WithTcpServer(config.Host, config.Port)
            .WithCleanSession(true)
            .WithCleanStart(true)
            .WithKeepAlivePeriod(TimeSpan.FromSeconds(config.KeepAliveSeconds))
            .WithWillQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce);

        if (!string.IsNullOrWhiteSpace(config.Username))
        {
            builder.WithCredentials(config.Username, config.Password);
        }

        return builder.Build();
    }

    private IEnumerable<MqttApplicationMessage> BuildDiscoveryMessages(DeviceConfig deviceConfig)
    {
        yield return BuildDiscoveryMessage(
            deviceConfig,
            "remaining_energy_kwh",
            "剩余电量",
            "{{ value_json.remaining_energy_kwh }}",
            unitOfMeasurement: "kWh",
            deviceClass: "energy",
            stateClass: null,
            icon: "mdi:battery-high");

        yield return BuildDiscoveryMessage(
            deviceConfig,
            "remaining_amount_cny",
            "剩余金额",
            "{{ value_json.remaining_amount_cny }}",
            unitOfMeasurement: _haConfig.Currency,
            deviceClass: "monetary",
            stateClass: null,
            icon: "mdi:cash");

        yield return BuildDiscoveryMessage(
            deviceConfig,
            "total_energy_consumed_kwh",
            "累计用电",
            "{{ value_json.total_energy_consumed_kwh }}",
            unitOfMeasurement: "kWh",
            deviceClass: "energy",
            stateClass: "total_increasing",
            icon: "mdi:transmission-tower");

        yield return BuildDiscoveryMessage(
            deviceConfig,
            "total_cost_spent_cny",
            "累计花费",
            "{{ value_json.total_cost_spent_cny }}",
            unitOfMeasurement: _haConfig.Currency,
            deviceClass: "monetary",
            stateClass: "total",
            icon: "mdi:cash-multiple");

        yield return BuildDiscoveryMessage(
            deviceConfig,
            "current_power_kw",
            "实时功率",
            "{{ value_json.current_power_kw }}",
            unitOfMeasurement: "kW",
            deviceClass: "power",
            stateClass: "measurement",
            icon: "mdi:lightning-bolt");
    }

    private MqttApplicationMessage BuildDiscoveryMessage(
        DeviceConfig deviceConfig,
        string objectId,
        string sensorName,
        string valueTemplate,
        string unitOfMeasurement,
        string? deviceClass,
        string? stateClass,
        string icon)
    {
        var stateTopic = $"{deviceConfig.Ha.BaseTopic.TrimEnd('/')}/state";
        var availabilityTopic = $"{deviceConfig.Ha.BaseTopic.TrimEnd('/')}/availability";

        var payload = new Dictionary<string, object?>
        {
            ["name"] = $"{deviceConfig.Ha.FriendlyName} {sensorName}",
            ["unique_id"] = $"{deviceConfig.Ha.DeviceId}_{objectId}",
            ["state_topic"] = stateTopic,
            ["availability_topic"] = availabilityTopic,
            ["payload_available"] = "online",
            ["payload_not_available"] = "offline",
            ["value_template"] = valueTemplate,
            ["unit_of_measurement"] = unitOfMeasurement,
            ["icon"] = icon,
            ["device"] = new Dictionary<string, object?>
            {
                ["identifiers"] = new[] { deviceConfig.Ha.DeviceId },
                ["name"] = deviceConfig.Ha.FriendlyName,
                ["manufacturer"] = deviceConfig.Ha.Manufacturer,
                ["model"] = deviceConfig.Ha.Model,
                ["configuration_url"] = deviceConfig.Config.TryGetValue("sourceUrl", out var val) ? val.GetString() : ""
            }
        };

        if (!string.IsNullOrWhiteSpace(deviceClass))
        {
            payload["device_class"] = deviceClass;
        }

        if (!string.IsNullOrWhiteSpace(stateClass))
        {
            payload["state_class"] = stateClass;
        }

        var topic =
            $"{_haConfig.DiscoveryPrefix.TrimEnd('/')}/sensor/{deviceConfig.Ha.DeviceId}/{objectId}/config";

        return BuildMessage(topic, JsonSerializer.Serialize(payload, MqttMessageJsonContext.Default.DictionaryStringObject), retain: true);
    }

    private static MqttApplicationMessage BuildMessage(string topic, string payload, bool retain)
    {
        return new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(Encoding.UTF8.GetBytes(payload))
            .WithRetainFlag(retain)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();
    }
}
