using System.Text.Json.Serialization;

namespace PowerMeterBridge;

public sealed class MqttStateMessage
{
    [JsonPropertyName("meter_id")]
    public string MeterId { get; set; } = string.Empty;

    [JsonPropertyName("observed_at")]
    public string ObservedAt { get; set; } = string.Empty;

    [JsonPropertyName("remaining_energy_kwh")]
    public decimal RemainingEnergyKwh { get; set; }

    [JsonPropertyName("remaining_amount_cny")]
    public decimal RemainingAmountCny { get; set; }

    [JsonPropertyName("price_cny_per_kwh")]
    public decimal PriceCnyPerKwh { get; set; }

    [JsonPropertyName("total_energy_consumed_kwh")]
    public decimal TotalEnergyConsumedKwh { get; set; }

    [JsonPropertyName("total_cost_spent_cny")]
    public decimal TotalCostSpentCny { get; set; }

    [JsonPropertyName("current_power_kw")]
    public decimal? CurrentPowerKw { get; set; }
}

[JsonSerializable(typeof(MqttStateMessage))]
[JsonSerializable(typeof(Dictionary<string, object?>))]
[JsonSourceGenerationOptions(
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
internal partial class MqttMessageJsonContext : JsonSerializerContext
{
}
