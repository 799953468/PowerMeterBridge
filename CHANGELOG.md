# Changelog

## [0.1.0] - 2026-11-10

First public release of PowerMeterBridge.

### Added

- Collect prepaid electricity meter readings from CNYIoT.
- Support multiple configured meters with isolated polling pipelines and per-device state files.
- Publish meter readings and Home Assistant MQTT Discovery entities, including energy, balance, consumption, cost, and current power metrics.
- Persist consumption history and calculate derived readings across meter recharges and restarts.
- Provide Docker deployment with multi-architecture image builds and native AOT binaries for Linux, macOS, and Windows.
- Add unit tests for meter readings and persisted bridge state.

### Changed

- Organize the application under `src/PowerMeterBridge/` and keep tests under `tests/`.
- Update Docker, solution, test, and release build paths for the reorganized project.

### Requirements

- .NET 11 SDK to build from source, or a release binary for the target platform.
- An MQTT broker and a configured CNYIoT meter.

### Validation

- `dotnet test PowerMeterBridge.slnx --configuration Release` (16 tests passed).
