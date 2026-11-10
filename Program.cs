using System.Net;
using System.Text;

namespace PowerMeterBridge;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        if (args.Length == 0 || !TryParseIntervalSeconds(args[0], out var pollIntervalSeconds))
        {
            await Console.Error.WriteLineAsync("用法：dotnet run --project PowerMeterBridge -- <刷新间隔秒数> [配置文件路径]");
            await Console.Error.WriteLineAsync("示例：dotnet run --project PowerMeterBridge -- 60");
            await Console.Error.WriteLineAsync("示例：dotnet run --project PowerMeterBridge -- 60 appsettings.json");
            return 1;
        }

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cts.Cancel();
        };

        LoadedAppConfig loadedConfig;
        try
        {
            loadedConfig = AppConfigLoader.Load(args);
        }
        catch (Exception exception)
        {
            await Console.Error.WriteLineAsync($"配置加载失败：{exception.Message}", cts.Token);
            return 1;
        }

        var config = loadedConfig.Config;
        var stateFilePath = ResolvePath(config.Storage.StateFilePath, loadedConfig.ConfigDirectory);
        var stateStore = new BridgeStateStore(stateFilePath);
        var state = await stateStore.LoadAsync(cts.Token);

        using var httpClient = CreateHttpClient(config.Http);
        var pageClient = new CnyiotPageClient(httpClient, config.SourceUrl);
        var publisher = new HomeAssistantMqttPublisher(config);

        Console.WriteLine($"已加载配置：{loadedConfig.ConfigPath}");
        Console.WriteLine($"状态文件：{stateFilePath}");
        Console.WriteLine($"轮询间隔：{pollIntervalSeconds} 秒");

        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    var reading = await pageClient.GetReadingAsync(cts.Token);
                    state.Apply(reading);
                    await stateStore.SaveAsync(state, cts.Token);
                    await publisher.PublishDiscoveryAsync(cts.Token);
                    await publisher.PublishStateAsync(reading, state, cts.Token);

                    Console.WriteLine(
                        $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] " +
                        $"表号={reading.MeterId} " +
                        $"剩余电量={reading.RemainingEnergyKwh:F2}kWh " +
                        $"剩余金额={reading.RemainingAmountCny:F2}{config.HomeAssistant.Currency} " +
                        $"累计用电={state.TotalConsumedEnergyKwh:F4}kWh " +
                        $"累计花费={state.TotalConsumedCostCny:F2}{config.HomeAssistant.Currency} " +
                        $"实时功率={state.CurrentPowerKw?.ToString("F3") ?? "N/A"}kW");

                    await Task.Delay(TimeSpan.FromSeconds(pollIntervalSeconds), cts.Token);
                }
                catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    Console.Error.WriteLine(
                        $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] 抓取或发布失败：{exception.Message}");
                    await Task.Delay(TimeSpan.FromSeconds(config.FailureRetrySeconds), cts.Token);
                }
            }
        }
        finally
        {
            try
            {
                await publisher.DisconnectAsync(CancellationToken.None);
            }
            catch
            {
                // 退出阶段不再阻断进程结束。
            }

            publisher.Dispose();
        }

        return 0;
    }

    private static HttpClient CreateHttpClient(HttpOptions config)
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All
        };

        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds)
        };

        if (!string.IsNullOrWhiteSpace(config.UserAgent))
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(config.UserAgent);
        }

        return client;
    }

    private static string ResolvePath(string path, string baseDirectory)
    {
        return Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(path, baseDirectory);
    }

    private static bool TryParseIntervalSeconds(string rawValue, out int intervalSeconds)
    {
        if (int.TryParse(rawValue, out intervalSeconds) && intervalSeconds > 0)
        {
            return true;
        }

        intervalSeconds = 0;
        return false;
    }
}
