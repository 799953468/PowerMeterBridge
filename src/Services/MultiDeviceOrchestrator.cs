namespace PowerMeterBridge;

/// <summary>
/// 多设备协调器
/// 管理所有设备的生命周期和并发轮询
/// </summary>
public sealed class MultiDeviceOrchestrator : IAsyncDisposable
{
    private readonly AppConfig _appConfig;
    private readonly HttpClient _httpClient;
    private readonly HomeAssistantMqttPublisher _publisher;
    private readonly Dictionary<string, MeterDevicePipeline> _pipelines = [];
    private readonly Dictionary<string, IMeterClientFactory> _factories;

    public MultiDeviceOrchestrator(
        AppConfig appConfig,
        HttpClient httpClient,
        HomeAssistantMqttPublisher publisher)
    {
        _appConfig = appConfig;
        _httpClient = httpClient;
        _publisher = publisher;
        _factories = RegisterFactories();
    }

    /// <summary>
    /// 初始化所有启用的设备
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        foreach (var deviceConfig in _appConfig.Devices.Where(d => d.Enabled))
        {
            try
            {
                // 获取对应品牌的工厂
                if (!_factories.TryGetValue(deviceConfig.Brand, out var factory))
                {
                    throw new InvalidOperationException(
                        $"不支持的设备品牌：'{deviceConfig.Brand}'");
                }

                // 创建设备客户端
                var client = factory.CreateClient(deviceConfig, _httpClient);

                // 创建状态存储
                var stateFilePath = ResolvePath(
                    Path.Combine(_appConfig.Storage.StateDirectory ?? "data", $"{deviceConfig.Id}.json"),
                    Directory.GetCurrentDirectory());

                var stateStore = new BridgeStateStore(stateFilePath);

                // 创建设备管道
                var pipeline = new MeterDevicePipeline(
                    deviceConfig, client, stateStore, _publisher);

                // 初始化客户端
                await pipeline.InitializeAsync(cancellationToken);

                _pipelines[deviceConfig.Id] = pipeline;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"初始化设备 '{deviceConfig.Id}' 失败：{ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// 并发轮询所有设备
    /// </summary>
    public async Task PollAllAsync(CancellationToken cancellationToken)
    {
        var tasks = _pipelines.Values
            .Select(p => SafeExecuteAsync(p, cancellationToken))
            .ToArray();

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// 安全地执行单个设备轮询，错误不会影响其他设备
    /// </summary>
    private static async Task SafeExecuteAsync(
        MeterDevicePipeline pipeline,
        CancellationToken cancellationToken)
    {
        try
        {
            await pipeline.PollOnceAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 正常取消，不记录为错误
            throw;
        }
        catch (Exception ex)
        {
            // 单个设备的错误记录但不抛出
            await Console.Error.WriteLineAsync(
                $"[{pipeline.DeviceId}] 轮询失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 清理所有资源
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        foreach (var pipeline in _pipelines.Values)
        {
            await pipeline.DisposeAsync();
        }
    }

    /// <summary>
    /// 注册所有支持的品牌工厂
    /// </summary>
    private static Dictionary<string, IMeterClientFactory> RegisterFactories()
    {
        return new(StringComparer.OrdinalIgnoreCase)
        {
            ["cnyiot"] = new CnyiotMeterClientFactory(),
            // 未来添加更多品牌
            // ["state_grid"] = new StateGridMeterClientFactory(),
            // ["local_grid"] = new LocalGridMeterClientFactory(),
        };
    }

    /// <summary>
    /// 解析相对路径为绝对路径
    /// </summary>
    private static string ResolvePath(string path, string baseDirectory)
    {
        return Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(path, baseDirectory);
    }
}
