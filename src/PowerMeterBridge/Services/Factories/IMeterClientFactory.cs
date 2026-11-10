namespace PowerMeterBridge;

/// <summary>
/// 电表客户端工厂接口
/// 根据设备配置创建对应品牌的客户端
/// </summary>
public interface IMeterClientFactory
{
    /// <summary>
    /// 根据设备配置创建电表客户端
    /// </summary>
    /// <param name="config">设备配置</param>
    /// <param name="httpClient">HTTP 客户端实例</param>
    /// <returns>创建的电表客户端</returns>
    /// <exception cref="ArgumentException">当配置无效时抛出</exception>
    IMeterClient CreateClient(DeviceConfig config, HttpClient httpClient);
}
