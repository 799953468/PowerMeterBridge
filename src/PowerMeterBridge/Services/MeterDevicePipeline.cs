namespace PowerMeterBridge;

/// <summary>
/// 单个电表设备的数据处理管道
/// 负责轮询数据、更新状态、发布到 MQTT
/// </summary>
public sealed class MeterDevicePipeline : IAsyncDisposable
{
    private readonly DeviceConfig _config;
    private readonly IMeterClient _client;
    private readonly BridgeStateStore _stateStore;
    private readonly HomeAssistantMqttPublisher _publisher;
    private bool _discoveryPublished;

    public string DeviceId => _config.Id;
    public string DeviceName => _config.Name;

    public MeterDevicePipeline(
        DeviceConfig config,
        IMeterClient client,
        BridgeStateStore stateStore,
        HomeAssistantMqttPublisher publisher)
    {
        _config = config;
        _client = client;
        _stateStore = stateStore;
        _publisher = publisher;
    }

    /// <summary>
    /// 初始化设备客户端
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await _client.InitializeAsync(cancellationToken);
        _discoveryPublished = false;
    }

    /// <summary>
    /// 执行一次轮询周期：获取数据 -> 更新状态 -> 发布
    /// </summary>
    public async Task PollOnceAsync(CancellationToken cancellationToken)
    {
        // 获取电表读数
        var reading = await _client.GetReadingAsync(cancellationToken);

        // 加载或初始化设备状态
        var state = await _stateStore.LoadAsync(cancellationToken);

        // 应用新的读数到状态
        state.Apply(reading);

        // 保存更新后的状态
        await _stateStore.SaveAsync(state, cancellationToken);

        // 发布 Home Assistant Discovery（首次发布）
        if (!_discoveryPublished)
        {
            await _publisher.PublishDiscoveryAsync(_config, cancellationToken);
            _discoveryPublished = true;
        }

        // 发布当前状态
        await _publisher.PublishStateAsync(_config, reading, state, cancellationToken);
    }

    /// <summary>
    /// 清理资源
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await _client.DisconnectAsync(CancellationToken.None);
    }
}
