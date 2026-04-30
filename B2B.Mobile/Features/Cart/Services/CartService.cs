using System.Collections.ObjectModel;
using System.Text.Json;
using B2B.Mobile.Core;
using B2B.Mobile.Features.Cart.Models;
using Microsoft.Maui.Storage;

namespace B2B.Mobile.Features.Cart.Services;

public sealed class CartService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ObservableCollection<CartLine> _lines = new();
    public ReadOnlyObservableCollection<CartLine> Lines { get; }

    public CartService()
    {
        Lines = new ReadOnlyObservableCollection<CartLine>(_lines);
        LoadFromPreferences();
    }

    public decimal Total => _lines.Sum(x => x.LineTotal);

    public void AddOrIncrement(CartLine line)
    {
        var existing = _lines.FirstOrDefault(x => x.ProductId == line.ProductId);
        if (existing is null)
        {
            _lines.Add(line);
        }
        else
        {
            _lines.Remove(existing);
            _lines.Add(existing with
            {
                Quantity = existing.Quantity + line.Quantity,
                SellerUserId = line.SellerUserId,
                SellerDisplayName = line.SellerDisplayName,
                Name = line.Name,
                Sku = line.Sku,
                CurrencyCode = line.CurrencyCode,
                UnitPrice = line.UnitPrice
            });
        }

        SaveToPreferences();
    }

    public void Increment(Guid productId, int amount = 1)
    {
        if (amount <= 0) return;
        var existing = _lines.FirstOrDefault(x => x.ProductId == productId);
        if (existing is null) return;
        _lines.Remove(existing);
        _lines.Add(existing with { Quantity = existing.Quantity + amount });
        SaveToPreferences();
    }

    public void Decrement(Guid productId, int amount = 1)
    {
        if (amount <= 0) return;
        var existing = _lines.FirstOrDefault(x => x.ProductId == productId);
        if (existing is null) return;

        var nextQty = existing.Quantity - amount;
        _lines.Remove(existing);
        if (nextQty > 0)
            _lines.Add(existing with { Quantity = nextQty });

        SaveToPreferences();
    }

    public void Remove(Guid productId)
    {
        var existing = _lines.FirstOrDefault(x => x.ProductId == productId);
        if (existing is not null)
        {
            _lines.Remove(existing);
            SaveToPreferences();
        }
    }

    public void Clear()
    {
        _lines.Clear();
        SaveToPreferences();
    }

    private void LoadFromPreferences()
    {
        try
        {
            var json = Preferences.Default.Get(MobilePreferenceKeys.CartLinesV1, "");
            if (string.IsNullOrWhiteSpace(json))
                return;

            var list = JsonSerializer.Deserialize<List<CartLine>>(json, JsonOptions);
            if (list is null || list.Count == 0)
                return;

            foreach (var line in list)
                _lines.Add(line);
        }
        catch
        {
            // Bozuk kayıt: boş sepetle devam
        }
    }

    private void SaveToPreferences()
    {
        try
        {
            var list = _lines.ToList();
            var json = JsonSerializer.Serialize(list, JsonOptions);
            Preferences.Default.Set(MobilePreferenceKeys.CartLinesV1, json);
        }
        catch
        {
            // Tercih yazılamazsa sepet yine de bellekte çalışır
        }
    }
}
