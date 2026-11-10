using FluentAssertions;

namespace PowerMeterBridge.Tests;

public class BridgeStateTests
{
    [Fact]
    public void BridgeState_DefaultValues_ShouldBeZeroOrNull()
    {
        // Act
        var state = new BridgeState();

        // Assert
        state.LastRemainingEnergyKwh.Should().BeNull();
        state.LastRemainingAmountCny.Should().BeNull();
        state.TotalConsumedEnergyKwh.Should().Be(0);
        state.TotalConsumedCostCny.Should().Be(0);
        state.CurrentPowerKw.Should().BeNull();
        state.LastObservedAt.Should().BeNull();
    }

    [Fact]
    public void Apply_FirstReading_ShouldSetInitialValues()
    {
        // Arrange
        var state = new BridgeState();
        var reading = new MeterReading
        {
            MeterId = "123456",
            RemainingEnergyKwh = 100m,
            RemainingAmountCurrency = 100m,
            PriceCurrencyPerKwh = 1.0m,
            ObservedAt = DateTimeOffset.Now
        };

        // Act
        state.Apply(reading);

        // Assert
        state.LastRemainingEnergyKwh.Should().Be(100m);
        state.LastRemainingAmountCny.Should().Be(100m);
        state.TotalConsumedEnergyKwh.Should().Be(0); // No consumption yet
        state.TotalConsumedCostCny.Should().Be(0);
        state.CurrentPowerKw.Should().BeNull(); // Can't calculate power on first reading
    }

    [Fact]
    public void Apply_SecondReading_ShouldCalculateConsumption()
    {
        // Arrange
        var state = new BridgeState();
        var reading1 = new MeterReading
        {
            MeterId = "123456",
            RemainingEnergyKwh = 100m,
            RemainingAmountCurrency = 100m,
            PriceCurrencyPerKwh = 1.0m,
            ObservedAt = DateTimeOffset.Now
        };
        var reading2 = new MeterReading
        {
            MeterId = "123456",
            RemainingEnergyKwh = 98m, // Consumed 2 kWh
            RemainingAmountCurrency = 98m, // Spent 2 CNY
            PriceCurrencyPerKwh = 1.0m,
            ObservedAt = reading1.ObservedAt.AddHours(1)
        };

        // Act
        state.Apply(reading1);
        state.Apply(reading2);

        // Assert
        state.TotalConsumedEnergyKwh.Should().Be(2m);
        state.TotalConsumedCostCny.Should().Be(2m);
        state.CurrentPowerKw.Should().Be(2m); // 2 kWh / 1 hour = 2 kW
    }

    [Fact]
    public void Apply_Recharge_ShouldNotAffectTotalConsumption()
    {
        // Arrange
        var state = new BridgeState();
        var reading1 = new MeterReading
        {
            MeterId = "123456",
            RemainingEnergyKwh = 10m,
            RemainingAmountCurrency = 10m,
            PriceCurrencyPerKwh = 1.0m,
            ObservedAt = DateTimeOffset.Now
        };
        var reading2 = new MeterReading
        {
            MeterId = "123456",
            RemainingEnergyKwh = 100m, // Recharged!
            RemainingAmountCurrency = 100m,
            PriceCurrencyPerKwh = 1.0m,
            ObservedAt = reading1.ObservedAt.AddMinutes(10)
        };

        // Act
        state.Apply(reading1);
        var previousTotal = state.TotalConsumedEnergyKwh;
        state.Apply(reading2);

        // Assert
        state.TotalConsumedEnergyKwh.Should().Be(previousTotal); // No increase after recharge
        state.LastRemainingEnergyKwh.Should().Be(100m);
    }

    [Fact]
    public void Apply_PowerCalculation_ShouldBeAccurate()
    {
        // Arrange
        var state = new BridgeState();
        var baseTime = new DateTimeOffset(2026, 11, 10, 10, 0, 0, TimeSpan.FromHours(8));

        var reading1 = new MeterReading
        {
            MeterId = "123456",
            RemainingEnergyKwh = 100m,
            RemainingAmountCurrency = 100m,
            PriceCurrencyPerKwh = 1.0m,
            ObservedAt = baseTime
        };

        var reading2 = new MeterReading
        {
            MeterId = "123456",
            RemainingEnergyKwh = 97.5m, // Consumed 2.5 kWh in 30 minutes
            RemainingAmountCurrency = 97.5m,
            PriceCurrencyPerKwh = 1.0m,
            ObservedAt = baseTime.AddMinutes(30)
        };

        // Act
        state.Apply(reading1);
        state.Apply(reading2);

        // Assert
        state.CurrentPowerKw.Should().Be(5m); // 2.5 kWh / 0.5 hour = 5 kW
    }

    [Fact]
    public void Apply_RoundingAccuracy_ShouldBeConsistent()
    {
        // Arrange
        var state = new BridgeState();
        var reading1 = new MeterReading
        {
            MeterId = "123456",
            RemainingEnergyKwh = 100.123456m,
            RemainingAmountCurrency = 100.123m,
            PriceCurrencyPerKwh = 1.0m,
            ObservedAt = DateTimeOffset.Now
        };
        var reading2 = new MeterReading
        {
            MeterId = "123456",
            RemainingEnergyKwh = 99.654321m,
            RemainingAmountCurrency = 99.876m,
            PriceCurrencyPerKwh = 1.0m,
            ObservedAt = reading1.ObservedAt.AddHours(1)
        };

        // Act
        state.Apply(reading1);
        state.Apply(reading2);

        // Assert
        state.TotalConsumedEnergyKwh.Should().BeApproximately(0.4691m, 0.0001m); // Rounded to 4 decimals
        state.TotalConsumedCostCny.Should().BeApproximately(0.25m, 0.01m); // Rounded to 2 decimals
    }
}
