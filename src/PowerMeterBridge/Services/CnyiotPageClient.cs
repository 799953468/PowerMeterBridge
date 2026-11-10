using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace PowerMeterBridge;

public sealed record CnyiotReading(
    string MeterId,
    decimal RemainingEnergyKwh,
    decimal RemainingAmountCny,
    decimal PriceCnyPerKwh,
    DateTimeOffset ObservedAt);

public sealed partial class CnyiotPageClient
{
    private static readonly Regex MeterIdRegex = new(
        @"id=""metid""[^>]*>\s*(?<value>\d+)\s*<",
        RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.CultureInvariant);

    private static readonly Regex RemainingEnergyRegex = new(
        @"剩余电量:\s*</span>\s*<label[^>]*>\s*(?<value>-?\d+(?:\.\d+)?)\s*</label>\s*<span>\s*(?<unit>[^<]+)\s*</span>",
        RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.CultureInvariant);

    private static readonly Regex RemainingAmountRegex = new(
        @"剩余金额:\s*</span>\s*<label[^>]*>\s*(?<value>-?\d+(?:\.\d+)?)\s*</label>\s*<span>\s*(?<unit>[^<]+)\s*</span>",
        RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.CultureInvariant);

    private static readonly Regex PriceRegex = new(
        @"综合费用:\s*</span>\s*<label[^>]*>\s*(?<value>-?\d+(?:\.\d+)?)\s*</label>\s*<span>\s*(?<unit>[^<]+)\s*</span>",
        RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.CultureInvariant);

    private readonly HttpClient _httpClient;
    private readonly string _sourceUrl;

    public CnyiotPageClient(HttpClient httpClient, string sourceUrl)
    {
        _httpClient = httpClient;
        _sourceUrl = sourceUrl;
    }

    public async Task<CnyiotReading> GetReadingAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, _sourceUrl);
        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseContentRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        return Parse(html);
    }

    public static CnyiotReading Parse(string html)
    {
        var normalizedHtml = WebUtility.HtmlDecode(html);
        var meterId = ExtractString(normalizedHtml, MeterIdRegex, "表号");
        var remainingEnergy = ExtractDecimal(normalizedHtml, RemainingEnergyRegex, "剩余电量");
        var remainingAmount = ExtractDecimal(normalizedHtml, RemainingAmountRegex, "剩余金额");
        var price = TryExtractDecimal(normalizedHtml, PriceRegex) ?? 0m;

        return new CnyiotReading(
            meterId,
            remainingEnergy,
            remainingAmount,
            price,
            DateTimeOffset.Now);
    }

    private static string ExtractString(string html, Regex regex, string fieldName)
    {
        var match = regex.Match(html);
        if (!match.Success)
        {
            throw new InvalidOperationException($"页面中找不到 {fieldName}，页面结构可能已变化。");
        }

        return match.Groups["value"].Value.Trim();
    }

    private static decimal ExtractDecimal(string html, Regex regex, string fieldName)
    {
        var match = regex.Match(html);
        if (!match.Success)
        {
            throw new InvalidOperationException($"页面中找不到 {fieldName}，页面结构可能已变化。");
        }

        return ParseDecimal(match.Groups["value"].Value, fieldName);
    }

    private static decimal? TryExtractDecimal(string html, Regex regex)
    {
        var match = regex.Match(html);
        return match.Success ? ParseDecimal(match.Groups["value"].Value, "综合费用") : null;
    }

    private static decimal ParseDecimal(string rawValue, string fieldName)
    {
        if (decimal.TryParse(rawValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        throw new InvalidOperationException($"{fieldName} 解析失败，原始值：{rawValue}");
    }
}
