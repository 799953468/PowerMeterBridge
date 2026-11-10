namespace PowerMeterBridge;

/// <summary>
/// 统一的电表读数模型
/// 所有品牌的电表客户端都必须输出这个格式
/// </summary>
public sealed record MeterReading
{
    /// <summary>电表号</summary>
    public required string MeterId { get; init; }

    /// <summary>剩余电量 (kWh)</summary>
    public required decimal RemainingEnergyKwh { get; init; }

    /// <summary>剩余金额（本币）</summary>
    public required decimal RemainingAmountCurrency { get; init; }

    /// <summary>单价（本币/kWh）</summary>
    public required decimal PriceCurrencyPerKwh { get; init; }

    /// <summary>观测时间</summary>
    public required DateTimeOffset ObservedAt { get; init; }
}
