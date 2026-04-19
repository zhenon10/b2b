using System.Globalization;
using System.Text.Json;

namespace B2B.Mobile.Core.Finance;

/// <summary>USD → TRY kuru (Frankfurter ECB tabanlı açık API, önbellekli).</summary>
public sealed class ExchangeRateService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private (string Text, DateTime AtUtc)? _cache;

    public ExchangeRateService(IHttpClientFactory httpFactory) =>
        _httpFactory = httpFactory;

    public async Task<string> GetUsdTryDisplayAsync(CancellationToken ct)
    {
        if (_cache is { } hit && DateTime.UtcNow - hit.AtUtc < TimeSpan.FromMinutes(30))
            return hit.Text;

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_cache is { } hit2 && DateTime.UtcNow - hit2.AtUtc < TimeSpan.FromMinutes(30))
                return hit2.Text;

            var client = _httpFactory.CreateClient("fx");
            using var resp = await client.GetAsync("v1/latest?from=USD&to=TRY", ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                return _cache?.Text ?? "—";

            await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
            if (!doc.RootElement.TryGetProperty("rates", out var rates) ||
                !rates.TryGetProperty("TRY", out var tryEl))
                return _cache?.Text ?? "—";

            var rate = tryEl.GetDecimal();
            var tr = CultureInfo.GetCultureInfo("tr-TR");
            var text = $"1 USD = {rate.ToString("N2", tr)} ₺";
            _cache = (text, DateTime.UtcNow);
            return text;
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
}
