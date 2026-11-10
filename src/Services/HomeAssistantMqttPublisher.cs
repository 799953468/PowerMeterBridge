using System.Text;
using System.Text.Json;
using MQTTnet;
using MQTTnet.Protocol;

namespace PowerMeterBridge;

public sealed class HomeAssistantMqttPublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    private readonly AppConfig _config;
    private readonly IMqttClient _client;
    private readonly MqttClientOptions _clientOptions;
    private bool _discoveryPublished;

    public HomeAssistantMqttPublisher(AppConfig config)
    {
        _config = config;
        _client = new MqttClientFactory().CreateMqttClient();
        _clientOptions = BuildClientOptions(config);
    }

    public async Task PublishDiscoveryAsync(CancellationToken cancellationToken)
    {
        await EnsureConnectedAsync(cancellationToken);

        if (_discoveryPublished)
        {
            return;
        }

        foreach (var message in BuildDiscoveryMessages())
        {
            await _client.PublishAsync(message, cancellationToken);
        }

        _discoveryPublished = true;
    }

    public async Task PublishStateAsync(CnyiotReading reading, BridgeState state, CancellationToken cancellationToken)
    {
        await EnsureConnectedAsync(cancellationToken);

        var payload = JsonSerializer.Serialize(
            new
            {
                meter_id = reading.MeterId,
                observed_at = reading.ObservedAt.ToString("O"),
                remaining_energy_kwh = decimal.Round(reading.RemainingEnergyKwh, 2),
                remaining_amount_cny = decimal.Round(reading.RemainingAmountCny, 2),
                price_cny_per_kwh = decimal.Round(reading.PriceCnyPerKwh, 4),
                total_energy_consumed_kwh = decimal.Round(state.TotalConsumedEnergyKwh, 4),
                total_cost_spent_cny = decimal.Round(state.TotalConsumedCostCny, 2),
                current_power_kw = state.CurrentPowerKw.HasValue ? decimal.Round(state.CurrentPowerKw.Value, 3) : (decimal?)null
            },
            JsonOptions);

        await _client.PublishAsync(
            BuildMessage(StateTopic, payload, retain: true),
            cancellationToken);

        await _client.PublishAsync(
            BuildMessage(AvailabilityTopic, "online", retain: true),
            cancellationToken);
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        if (!_client.IsConnected)
        {
            return;
        }

        await _client.PublishAsync(
            BuildMessage(AvailabilityTopic, "offline", retain: true),
            cancellationToken);

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
        await _client.PublishAsync(
            BuildMessage(AvailabilityTopic, "online", retain: true),
            cancellationToken);
        _discoveryPublished = false;
    }

    private MqttClientOptions BuildClientOptions(AppConfig config)
    {
        var builder = new MqttClientOptionsBuilder()
            .WithClientId(config.Mqtt.ClientId)
            .WithTcpServer(config.Mqtt.Host, config.Mqtt.Port)
            .WithCleanSession(true)
            .WithCleanStart(true)
            .WithKeepAlivePeriod(TimeSpan.FromSeconds(config.Mqtt.KeepAliveSeconds))
            .WithWillTopic(AvailabilityTopic)
            .WithWillPayload(Encoding.UTF8.GetBytes("offline"))
            .WithWillRetain(true)
            .WithWillQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce);

        if (!string.IsNullOrWhiteSpace(config.Mqtt.Username))
        {
            builder.WithCredentials(config.Mqtt.Username, config.Mqtt.Password);
        }

        return builder.Build();
    }

    private IEnumerable<MqttApplicationMessage> BuildDiscoveryMessages()
    {
        yield return BuildDiscoveryMessage(
            "remaining_energy_kwh",
            "剩余电量",
            "{{ value_json.remaining_energy_kwh }}",
            unitOfMeasurement: "kWh",
            deviceClass: "energy",
            stateClass: null,
            icon: "mdi:battery-high");

        yield return BuildDiscoveryMessage(
            "remaining_amount_cny",
            "剩余金额",
            "{{ value_json.remaining_amount_cny }}",
            unitOfMeasurement: _config.HomeAssistant.Currency,
            deviceClass: "monetary",
            stateClass: null,
            icon: "mdi:cash");

        yield return BuildDiscoveryMessage(
            "total_energy_consumed_kwh",
            "累计用电",
            "{{ value_json.total_energy_consumed_kwh }}",
            unitOfMeasurement: "kWh",
            deviceClass: "energy",
            stateClass: "total_increasing",
            icon: "mdi:transmission-tower");

        yield return BuildDiscoveryMessage(
            "total_cost_spent_cny",
            "累计花费",
            "{{ value_json.total_cost_spent_cny }}",
            unitOfMeasurement: _config.HomeAssistant.Currency,
            deviceClass: "monetary",
            stateClass: "total",
            icon: "mdi:cash-multiple");

        yield return BuildDiscoveryMessage(
            "current_power_kw",
            "实时功率",
            "{{ value_json.current_power_kw }}",
            unitOfMeasurement: "kW",
            deviceClass: "power",
            stateClass: "measurement",
            icon: "mdi:lightning-bolt");
    }

    private MqttApplicationMessage BuildDiscoveryMessage(
        string objectId,
        string sensorName,
        string valueTemplate,
        string unitOfMeasurement,
        string? deviceClass,
        string? stateClass,
        string icon)
    {
        var payload = new Dictionary<string, object?>
        {
            ["name"] = $"{_config.HomeAssistant.FriendlyName} {sensorName}",
            ["unique_id"] = $"{_config.HomeAssistant.DeviceId}_{objectId}",
            ["state_topic"] = StateTopic,
            ["availability_topic"] = AvailabilityTopic,
            ["payload_available"] = "online",
            ["payload_not_available"] = "offline",
            ["value_template"] = valueTemplate,
            ["unit_of_measurement"] = unitOfMeasurement,
            ["icon"] = icon,
            ["device"] = new Dictionary<string, object?>
            {
                ["identifiers"] = new[] { _config.HomeAssistant.DeviceId },
                ["name"] = _config.HomeAssistant.FriendlyName,
                ["manufacturer"] = _config.HomeAssistant.Manufacturer,
                ["model"] = _config.HomeAssistant.Model,
                ["configuration_url"] = _config.SourceUrl
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
            $"{_config.HomeAssistant.DiscoveryPrefix.TrimEnd('/')}/sensor/{_config.HomeAssistant.DeviceId}/{objectId}/config";

        return BuildMessage(topic, JsonSerializer.Serialize(payload, JsonOptions), retain: true);
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

    private string StateTopic => $"{_config.HomeAssistant.BaseTopic.TrimEnd('/')}/state";

    private string AvailabilityTopic => $"{_config.HomeAssistant.BaseTopic.TrimEnd('/')}/availability";
}
