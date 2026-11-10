namespace PowerMeterBridge;

public sealed class BridgeState
{
    public decimal? LastRemainingEnergyKwh { get; set; }
    public decimal? LastRemainingAmountCny { get; set; }
    public decimal TotalConsumedEnergyKwh { get; set; }
    public decimal TotalConsumedCostCny { get; set; }
    public DateTimeOffset? LastObservedAt { get; set; }
    public decimal? CurrentPowerKw { get; set; }

    public void Apply(MeterReading reading)
    {
        // 页面只有”剩余值”，累计用电/花费需要靠本地状态在每次轮询间推导。
        if (LastRemainingEnergyKwh is decimal previousEnergy && previousEnergy > reading.RemainingEnergyKwh)
        {
            TotalConsumedEnergyKwh += decimal.Round(previousEnergy - reading.RemainingEnergyKwh, 4);
        }

        if (LastRemainingAmountCny is decimal previousAmount && previousAmount > reading.RemainingAmountCurrency)
        {
            TotalConsumedCostCny += decimal.Round(previousAmount - reading.RemainingAmountCurrency, 2);
        }

        // 计算实时功率：功率 (kW) = 电量差 (kWh) / 时间差 (h)
        if (LastRemainingEnergyKwh is decimal prevEnergy
            && LastObservedAt is DateTimeOffset prevTime
            && prevEnergy > reading.RemainingEnergyKwh)
        {
            var energyConsumed = prevEnergy - reading.RemainingEnergyKwh;
            var timeElapsedHours = (decimal)(reading.ObservedAt - prevTime).TotalHours;

            if (timeElapsedHours > 0)
            {
                CurrentPowerKw = decimal.Round(energyConsumed / timeElapsedHours, 3);
            }
        }

        LastRemainingEnergyKwh = reading.RemainingEnergyKwh;
        LastRemainingAmountCny = reading.RemainingAmountCurrency;
        LastObservedAt = reading.ObservedAt;
        TotalConsumedEnergyKwh = decimal.Round(TotalConsumedEnergyKwh, 4);
        TotalConsumedCostCny = decimal.Round(TotalConsumedCostCny, 2);
    }
}
