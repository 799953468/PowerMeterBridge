# PowerMeterBridge

A high-performance MQTT bridge service that scrapes CNYIoT prepaid electric meter readings and publishes them to Home Assistant via MQTT with automatic discovery support.

## Features

- 🔄 **Automatic Polling**: Continuously fetch real-time meter readings at configurable intervals
- 🏠 **Home Assistant Integration**: Full MQTT Discovery support for seamless integration
- 📊 **Rich Metrics**: Tracks remaining balance, power consumption, real-time power usage, and cumulative statistics
- ⚡ **Native AOT Compilation**: Optimized for minimal memory footprint and fast startup
- 💾 **Persistent State**: Maintains consumption history and calculates derived metrics
- 🔒 **Robust Error Handling**: Automatic retry logic with configurable intervals

## Architecture

Built with .NET 11.0 and AOT compilation for maximum performance:

- **Native AOT Publishing**: Produces self-contained, platform-specific binaries with minimal overhead
- **Source-Generated JSON**: Uses `System.Text.Json` with source generators for AOT compatibility
- **Minimal Dependencies**: Only requires `MQTTnet` (v5.2.0) for MQTT communication

## Prerequisites

- .NET 11 SDK (for building from source)
- MQTT broker (e.g., Mosquitto)
- Access to a CNYIoT prepaid electric meter web interface
- (Optional) Home Assistant instance for full integration

## Installation

### Build from Source

```bash
# Clone the repository
git clone <repository-url>
cd PowerMeterBridge

# Restore dependencies
dotnet restore

# Build and run (debug mode)
dotnet run --project src/PowerMeterBridge/PowerMeterBridge.csproj -- 60

# Publish as native AOT binary (production)
dotnet publish src/PowerMeterBridge/PowerMeterBridge.csproj -c Release -r linux-x64 -o ./publish
./publish/PowerMeterBridge 60
```

### Runtime Platforms

Publish for your target platform:

- **Linux (x64)**: `-r linux-x64`
- **Linux (ARM64)**: `-r linux-arm64`
- **macOS (Apple Silicon)**: `-r osx-arm64`
- **macOS (Intel)**: `-r osx-x64`
- **Windows**: `-r win-x64`

## Configuration

### Initial Setup

1. Copy the example configuration:
   ```bash
   cp appsettings.json.example appsettings.json
   ```

2. Edit `appsettings.json` with your settings:

```json
{
  "SourceUrl": "http://www.wap.cnyiot.com/nat/pay.aspx?mid=YOUR_METER_ID",
  "FailureRetrySeconds": 60,
  "Http": {
    "TimeoutSeconds": 20,
    "UserAgent": "Mozilla/5.0 (compatible; CnyiotPowerMqttBridge/1.0)"
  },
  "Mqtt": {
    "Host": "192.168.1.10",
    "Port": 1883,
    "ClientId": "cnyiot-power-mqtt-bridge",
    "Username": "mqtt-user",
    "Password": "mqtt-password",
    "KeepAliveSeconds": 60
  },
  "HomeAssistant": {
    "DiscoveryPrefix": "homeassistant",
    "DeviceId": "cnyiot_meter_YOUR_METER_ID",
    "FriendlyName": "My Electric Meter",
    "BaseTopic": "home/cnyiot/YOUR_METER_ID",
    "Currency": "CNY",
    "Manufacturer": "CNYIoT",
    "Model": "Prepaid Electric Meter"
  },
  "Storage": {
    "StateFilePath": "data/bridge-state.json"
  }
}
```

### Configuration Reference

| Section | Key | Description | Default |
|---------|-----|-------------|---------|
| **Root** | `SourceUrl` | CNYIoT meter web interface URL | *Required* |
| | `FailureRetrySeconds` | Retry delay after errors | `60` |
| **Http** | `TimeoutSeconds` | HTTP request timeout | `20` |
| | `UserAgent` | User-Agent header | Browser-like string |
| **Mqtt** | `Host` | MQTT broker hostname | *Required* |
| | `Port` | MQTT broker port | `1883` |
| | `ClientId` | MQTT client identifier | *Required* |
| | `Username` | MQTT authentication username | *Required* |
| | `Password` | MQTT authentication password | *Required* |
| | `KeepAliveSeconds` | MQTT keep-alive interval | `60` |
| **HomeAssistant** | `DiscoveryPrefix` | MQTT discovery prefix | `homeassistant` |
| | `DeviceId` | Unique device identifier | *Required* |
| | `FriendlyName` | Display name in Home Assistant | *Required* |
| | `BaseTopic` | Base MQTT topic for state | *Required* |
| | `Currency` | Currency for cost metrics | `CNY` |
| | `Manufacturer` | Device manufacturer | `CNYIoT` |
| | `Model` | Device model name | `Prepaid Electric Meter` |
| **Storage** | `StateFilePath` | Path for persistent state | `data/bridge-state.json` |

## Usage

### Basic Usage

```bash
# Run with 60-second polling interval
dotnet run --project src/PowerMeterBridge/PowerMeterBridge.csproj -- 60

# Run with custom config file
dotnet run --project src/PowerMeterBridge/PowerMeterBridge.csproj -- 60 /path/to/config.json

# Production (AOT binary)
./PowerMeterBridge 60
```

### Command-Line Arguments

```
PowerMeterBridge <poll_interval_seconds> [config_file_path]
```

- **`poll_interval_seconds`** (required): Interval between meter readings in seconds (must be > 0)
- **`config_file_path`** (optional): Path to configuration file (defaults to `appsettings.json`)

### Example Output

```
Loaded configuration: appsettings.json
State file: /app/data/bridge-state.json
Polling interval: 60 seconds
[2026-11-10 14:30:15] MeterId=19000751693 RemainingEnergy=125.45kWh RemainingAmount=125.45CNY TotalConsumed=1523.8642kWh TotalCost=1523.86CNY CurrentPower=2.450kW
[2026-11-10 14:31:15] MeterId=19000751693 RemainingEnergy=125.44kWh RemainingAmount=125.44CNY TotalConsumed=1523.8743kWh TotalCost=1523.87CNY CurrentPower=2.436kW
```

## MQTT Topics & Payloads

### Discovery Topics

The bridge publishes Home Assistant MQTT Discovery messages for automatic entity creation:

- `homeassistant/sensor/{device_id}_remaining_energy/config`
- `homeassistant/sensor/{device_id}_remaining_balance/config`
- `homeassistant/sensor/{device_id}_total_consumed/config`
- `homeassistant/sensor/{device_id}_total_cost/config`
- `homeassistant/sensor/{device_id}_current_power/config`
- `homeassistant/sensor/{device_id}_price/config`

### State Topic

State updates are published to: `{BaseTopic}/state`

Example payload:
```json
{
  "meter_id": "19000751693",
  "remaining_energy_kwh": 125.45,
  "remaining_balance_cny": 125.45,
  "total_consumed_kwh": 1523.8642,
  "total_cost_cny": 1523.86,
  "current_power_kw": 2.450,
  "price_cny_per_kwh": 1.0,
  "last_updated": "2026-11-10T14:30:15+08:00"
}
```

## Home Assistant Integration

Once running, the bridge will automatically create the following entities in Home Assistant:

- **Remaining Energy** (kWh): How much prepaid electricity is left
- **Remaining Balance** (CNY): Monetary balance remaining
- **Total Energy Consumed** (kWh): Lifetime cumulative consumption
- **Total Cost** (CNY): Lifetime cumulative cost
- **Current Power** (kW): Real-time power consumption rate
- **Electricity Price** (CNY/kWh): Current electricity rate

All entities will appear under a single device named according to your `FriendlyName` configuration.

## Persistent State

The bridge maintains state in `data/bridge-state.json`:

```json
{
  "last_remaining_energy_kwh": 125.45,
  "last_remaining_amount_cny": 125.45,
  "total_consumed_energy_kwh": 1523.8642,
  "total_consumed_cost_cny": 1523.86,
  "current_power_kw": 2.450,
  "last_updated": "2026-11-10T14:30:15+08:00"
}
```

This file is automatically created and updated. It enables the bridge to:

- Calculate cumulative consumption across restarts
- Compute real-time power usage from consumption deltas
- Preserve history even if the meter is recharged

## Error Handling

The bridge includes robust error handling:

- **Network Failures**: Automatic retry after `FailureRetrySeconds`
- **Parse Errors**: Logged with retry on next poll cycle
- **MQTT Disconnections**: Automatic reconnection attempts
- **Graceful Shutdown**: Ctrl+C cleanly disconnects MQTT and saves state

## Development

### Project Structure

```
PowerMeterBridge/
├── PowerMeterBridge.slnx               # Solution
├── appsettings.json                    # Configuration file
├── appsettings.json.example            # Configuration template
├── Dockerfile
├── docker-compose.yml
├── data/
│   └── bridge-state.json               # Persistent state
└── src/
    └── PowerMeterBridge/
        ├── PowerMeterBridge.csproj     # Application project
        ├── Program.cs                  # Entry point and main loop
        ├── Configuration/              # Application configuration
        ├── Models/                     # Domain and state models
        └── Services/                   # Meter clients, orchestration, MQTT
tests/
└── PowerMeterBridge.Tests/             # Unit tests
```

### Adding Features

The codebase uses:

- **Source-generated JSON**: Add new properties to `*JsonContext.cs` files
- **Regex-based scraping**: Update patterns in `CnyiotPageClient.cs`
- **MQTT entity types**: Extend `HomeAssistantMqttPublisher.cs` for new sensors

## Troubleshooting

### Common Issues

**Problem**: "Configuration load failed"
- **Solution**: Verify `appsettings.json` exists and is valid JSON

**Problem**: No data appearing in Home Assistant
- **Solution**: Check that `DiscoveryPrefix` matches your Home Assistant MQTT configuration (usually `homeassistant`)

**Problem**: "Scraping failed" errors
- **Solution**: Verify the `SourceUrl` is correct and accessible from your network

**Problem**: MQTT connection failures
- **Solution**: Verify MQTT broker is running and credentials are correct

### Debug Mode

For development and debugging:

```bash
dotnet run --project src/PowerMeterBridge/PowerMeterBridge.csproj --configuration Debug -- 60
```

## License

[Specify your license here]

## Contributing

Contributions are welcome! Please open an issue or submit a pull request.

## Acknowledgments

- Built with [MQTTnet](https://github.com/dotnet/MQTTnet)
- Designed for [Home Assistant](https://www.home-assistant.io/)
