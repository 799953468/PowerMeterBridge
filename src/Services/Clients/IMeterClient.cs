namespace PowerMeterBridge;

/// <summary>
/// 电表客户端接口
/// 所有品牌的电表实现都必须实现此接口
/// </summary>
public interface IMeterClient
{
    /// <summary>品牌标识（如 "cnyiot", "state_grid"）</summary>
    string Brand { get; }

    /// <summary>
    /// 获取电表数据
    /// </summary>
    /// <exception cref="InvalidOperationException">当数据格式无法解析或网络请求失败时抛出</exception>
    ValueTask<MeterReading> GetReadingAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 初始化客户端（可选）
    /// 用于验证连接、初始化认证等
    /// </summary>
    ValueTask InitializeAsync(CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;

    /// <summary>
    /// 断开连接并清理资源（可选）
    /// </summary>
    ValueTask DisconnectAsync(CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;
}
