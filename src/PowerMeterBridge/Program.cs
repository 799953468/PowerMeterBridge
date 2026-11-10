using System.Net;
using System.Text;

namespace PowerMeterBridge;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        // 配置文件路径（默认 appsettings.json）
        var configPath = args.Length > 0 ? args[0] : "appsettings.json";

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cts.Cancel();
        };

        LoadedAppConfig loadedConfig;
        try
        {
            loadedConfig = AppConfigLoader.Load(new[] { configPath });
        }
        catch (Exception exception)
        {
            await Console.Error.WriteLineAsync($"配置加载失败：{exception.Message}");
            return 1;
        }

        var config = loadedConfig.Config;

        // 验证配置
        try
        {
            config.Validate();
        }
        catch (Exception exception)
        {
            await Console.Error.WriteLineAsync($"配置验证失败：{exception.Message}");
            return 1;
        }

        // 创建 HTTP 客户端（用于网络请求类型的设备）
        using var httpClient = CreateHttpClient();

        // 创建 MQTT 发布器
        var publisher = new HomeAssistantMqttPublisher(config.Mqtt, config.HomeAssistant);

        // 创建多设备协调器
        var orchestrator = new MultiDeviceOrchestrator(config, httpClient, publisher);

        Console.WriteLine($"已加载配置：{loadedConfig.ConfigPath}");
        Console.WriteLine($"轮询间隔：{config.PollIntervalSeconds} 秒");
        Console.WriteLine($"失败重试间隔：{config.FailureRetrySeconds} 秒");

        try
        {
            // 初始化所有设备
            await orchestrator.InitializeAsync(cts.Token);

            var enabledDevices = config.Devices.Where(d => d.Enabled).ToList();
            Console.WriteLine($"已初始化 {enabledDevices.Count} 个设备：");
            foreach (var device in enabledDevices)
            {
                Console.WriteLine($"  ✓ {device.Id,-20} {device.Name,-20} ({device.Brand})");
            }

            Console.WriteLine();

            // 主轮询循环
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    // 并发轮询所有设备
                    await orchestrator.PollAllAsync(cts.Token);

                    // 计划下一次轮询
                    await Task.Delay(
                        TimeSpan.FromSeconds(config.PollIntervalSeconds),
                        cts.Token);
                }
                catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    Console.Error.WriteLine(
                        $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] 轮询失败：{exception.Message}");
                    await Task.Delay(
                        TimeSpan.FromSeconds(config.FailureRetrySeconds),
                        cts.Token);
                }
            }
        }
        finally
        {
            try
            {
                await orchestrator.DisposeAsync();
                await publisher.DisconnectAsync(CancellationToken.None);
            }
            catch
            {
                // 退出阶段不再阻断进程结束
            }

            publisher.Dispose();
        }

        return 0;
    }

    private static HttpClient CreateHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All
        };

        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(20)
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (compatible; PowerMeterBridge/2.0; +https://github.com/dotnet)");

        return client;
    }
}

