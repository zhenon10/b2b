using System.Collections.ObjectModel;
using B2B.Mobile.Features.Cart.Models;

namespace B2B.Mobile.Features.Cart.Services;

public sealed class CartService
{
    private readonly ObservableCollection<CartLine> _lines = new();
    public ReadOnlyObservableCollection<CartLine> Lines { get; }

    public CartService()
    {
        Lines = new ReadOnlyObservableCollection<CartLine>(_lines);
    }

    public decimal Total => _lines.Sum(x => x.LineTotal);

    public void AddOrIncrement(CartLine line)
    {
        var existing = _lines.FirstOrDefault(x => x.ProductId == line.ProductId);
        if (existing is null)
        {
            _lines.Add(line);
            return;
        }

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

    public void Remove(Guid productId)
    {
        var existing = _lines.FirstOrDefault(x => x.ProductId == productId);
        if (existing is not null)
            _lines.Remove(existing);
    }

    public void Clear() => _lines.Clear();
}

