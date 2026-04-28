using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace B2B.Mobile.Core.Finance;

/// <summary>
/// USD → TRY kuru (öncelik: AltinAPI/HaremAPI satış; yedek: Frankfurter), önbellekli.
/// </summary>
public sealed class ExchangeRateService
{
    private const string AltinApiKeyConfigKey = "Fx:AltinApi:ApiKey";

    private static readonly Uri[] UsdTryUris =
    [
        new("https://api.frankfurter.dev/v1/latest?from=USD&to=TRY", UriKind.Absolute),
        // `api.frankfurter.app` currently exposes `/latest` (no `/v1/...`).
        new("https://api.frankfurter.app/latest?from=USD&to=TRY", UriKind.Absolute),
    ];

    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private (string Text, DateTime AtUtc)? _cache;

    public ExchangeRateService(IHttpClientFactory httpFactory, IConfiguration config)
    {
        _httpFactory = httpFactory;
        _config = config;
    }

    public async Task<string> GetUsdTryDisplayAsync(CancellationToken ct)
    {
        if (_cache is { } hit && DateTime.UtcNow - hit.AtUtc < TimeSpan.FromMinutes(30))
            return hit.Text;

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_cache is { } hit2 && DateTime.UtcNow - hit2.AtUtc < TimeSpan.FromMinutes(30))
                return hit2.Text;

            // 1) AltinAPI (HaremAPI) — satış (ask)
            var altinKey = _config[AltinApiKeyConfigKey];
            if (!string.IsNullOrWhiteSpace(altinKey))
            {
                var altinClient = _httpFactory.CreateClient("altinapi");
                var altin = await TryGetAltinApiUsdTryAskAsync(altinClient, altinKey, ct).ConfigureAwait(false);
                if (altin is not null)
                {
                    _cache = (altin.Value.Text, DateTime.UtcNow);
                    return altin.Value.Text;
                }
            }

            // 2) Frankfurter fallback
            var client = _httpFactory.CreateClient("fx");
            const int maxAttempts = 3;
            foreach (var uri in UsdTryUris)
            {
                HttpResponseMessage? resp = null;
                for (var attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    try
                    {
                        var r = await client.GetAsync(uri, ct).ConfigureAwait(false);
                        if ((int)r.StatusCode is >= 500 and < 600 && attempt < maxAttempts)
                        {
                            r.Dispose();
                            await Task.Delay(350 * (1 << (attempt - 1)), ct).ConfigureAwait(false);
                            continue;
                        }

                        if ((int)r.StatusCode is 404 or 410 && attempt == 1)
                        {
                            // Fast-fail on dead endpoints; caller will try the next mirror.
                            r.Dispose();
                            resp = null;
                            break;
                        }

                        resp = r;
                        break;
                    }
                    catch (HttpRequestException) when (attempt < maxAttempts)
                    {
                        await Task.Delay(350 * (1 << (attempt - 1)), ct).ConfigureAwait(false);
                    }
                    catch (TaskCanceledException) when (attempt < maxAttempts && !ct.IsCancellationRequested)
                    {
                        await Task.Delay(350 * (1 << (attempt - 1)), ct).ConfigureAwait(false);
                    }
                }

                if (resp is null)
                    continue;

                using (resp)
                {
                    if (!resp.IsSuccessStatusCode)
                        continue;

                    await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                    using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
                    if (!doc.RootElement.TryGetProperty("rates", out var rates) ||
                        !rates.TryGetProperty("TRY", out var tryEl))
                        continue;

                    var rate = tryEl.GetDecimal();
                    var tr = CultureInfo.GetCultureInfo("tr-TR");
                    var text = $"USD/TRY (satış): {rate.ToString("N2", tr)} ₺";
                    _cache = (text, DateTime.UtcNow);
                    return text;
                }
            }

            return _cache?.Text ?? "—";
        }
        catch
        {
            return _cache?.Text ?? "—";
        }
        finally
        {
            _gate.Release();
        }
    }

    private static async Task<(string Text, decimal Ask)?> TryGetAltinApiUsdTryAskAsync(
        HttpClient client,
        string apiKey,
        CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "prices/USDTRY");
            req.Headers.TryAddWithoutValidation("X-API-Key", apiKey.Trim());

            using var resp = await client.SendAsync(req, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                return null;

            await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

            // Response shape: { symbol, category, bid, ask, timestamp }
            if (!doc.RootElement.TryGetProperty("ask", out var askEl))
                return null;

            var ask = askEl.GetDecimal();
            var tr = CultureInfo.GetCultureInfo("tr-TR");
            var text = $"USD/TRY (satış): {ask.ToString("N2", tr)} ₺";
            return (text, ask);
        }
        catch
        {
            return null;
        }
    }
}
