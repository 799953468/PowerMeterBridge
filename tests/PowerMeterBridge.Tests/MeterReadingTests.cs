using FluentAssertions;

namespace PowerMeterBridge.Tests;

public class MeterReadingTests
{
    [Fact]
    public void MeterReading_AllProperties_ShouldBeSet()
    {
        // Arrange
        var now = DateTimeOffset.Now;

        // Act
        var reading = new MeterReading
        {
            MeterId = "19000751693",
            RemainingEnergyKwh = 125.45m,
            RemainingAmountCurrency = 125.45m,
            PriceCurrencyPerKwh = 1.0m,
            ObservedAt = now
        };

        // Assert
        reading.MeterId.Should().Be("19000751693");
        reading.RemainingEnergyKwh.Should().Be(125.45m);
        reading.RemainingAmountCurrency.Should().Be(125.45m);
        reading.PriceCurrencyPerKwh.Should().Be(1.0m);
        reading.ObservedAt.Should().Be(now);
    }

    [Fact]
    public void MeterReading_RecordEquality_ShouldWork()
    {
        // Arrange
        var now = DateTimeOffset.Now;
        var reading1 = new MeterReading
        {
            MeterId = "123",
            RemainingEnergyKwh = 100m,
            RemainingAmountCurrency = 100m,
            PriceCurrencyPerKwh = 1.0m,
            ObservedAt = now
        };
        var reading2 = new MeterReading
        {
            MeterId = "123",
            RemainingEnergyKwh = 100m,
            RemainingAmountCurrency = 100m,
            PriceCurrencyPerKwh = 1.0m,
            ObservedAt = now
        };
        var reading3 = new MeterReading
        {
            MeterId = "456",
            RemainingEnergyKwh = 100m,
            RemainingAmountCurrency = 100m,
            PriceCurrencyPerKwh = 1.0m,
            ObservedAt = now
        };

        // Assert
        reading1.Should().Be(reading2);
        reading1.Should().NotBe(reading3);
    }

    [Fact]
    public void MeterReading_WithDecimalPrecision_ShouldMaintainAccuracy()
    {
        // Act
        var reading = new MeterReading
        {
            MeterId = "123",
            RemainingEnergyKwh = 123.456789m,
            RemainingAmountCurrency = 789.123456m,
            PriceCurrencyPerKwh = 1.234567m,
            ObservedAt = DateTimeOffset.Now
        };

        // Assert
        reading.RemainingEnergyKwh.Should().Be(123.456789m);
        reading.RemainingAmountCurrency.Should().Be(789.123456m);
        reading.PriceCurrencyPerKwh.Should().Be(1.234567m);
    }
}
