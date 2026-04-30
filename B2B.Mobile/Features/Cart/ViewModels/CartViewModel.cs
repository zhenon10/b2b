using System.Collections.ObjectModel;
using System.Collections.Specialized;
using B2B.Mobile.Features.Cart.Models;
using B2B.Mobile.Features.Cart.Services;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace B2B.Mobile.Features.Cart.ViewModels;

public partial class CartViewModel : ObservableObject
{
    private readonly CartService _cart;

    public ObservableCollection<CartLine> Lines { get; } = new();

    [ObservableProperty] private decimal total;
    [ObservableProperty] private string totalSummary = "0";

    public CartViewModel(CartService cart)
    {
        _cart = cart;
        if (_cart.Lines is INotifyCollectionChanged n)
            n.CollectionChanged += (_, _) => MainThread.BeginInvokeOnMainThread(Sync);
        Sync();
    }

    [RelayCommand]
    private void Remove(CartLine line)
    {
        _cart.Remove(line.ProductId);
        Sync();
    }

    [RelayCommand]
    private void Increment(CartLine line)
    {
        _cart.Increment(line.ProductId, 1);
        Sync();
    }

    [RelayCommand]
    private void Decrement(CartLine line)
    {
        var willRemove = line.Quantity <= 1;
        _cart.Decrement(line.ProductId, 1);
        Sync();

        if (!willRemove)
            return;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            var removed = line;
            var snack = Snackbar.Make(
                "Sepetten çıkarıldı",
                () =>
                {
                    _cart.AddOrIncrement(removed with { Quantity = 1 });
                    Sync();
                },
                "Geri al",
                TimeSpan.FromSeconds(3),
                new SnackbarOptions
                {
                    BackgroundColor = Color.FromArgb("#1F1F1F"),
                    TextColor = Colors.White,
                    ActionButtonTextColor = Color.FromArgb("#AC99EA")
                });
            await snack.Show();
        });
    }

    [RelayCommand]
    private void Clear()
    {
        _cart.Clear();
        Sync();
    }

    [RelayCommand]
    private async Task CheckoutAsync()
    {
        if (_cart.Lines.Count == 0)
        {
            await Shell.Current.DisplayAlertAsync("Sepet", "Sepetiniz boş.", "Tamam");
            return;
        }

        var currencies = _cart.Lines.Select(x => x.CurrencyCode).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (currencies.Count != 1)
        {
            await Shell.Current.DisplayAlertAsync(
                "Sepet",
                "Sepetiniz birden fazla para birimi içeriyor. Lütfen aynı para birimindeki ürünlerle sipariş verin.",
                "Tamam");
            return;
        }

        var sellerCount = _cart.Lines.Select(x => x.SellerUserId).Distinct().Count();
        if (sellerCount > 1)
        {
            await Shell.Current.DisplayAlertAsync(
                "Sepet",
                "Sepette tek satıcıya ait ürünler olmalıdır. Farklı satıcıların ürünlerini ayırın.",
                "Tamam");
            return;
        }

        await Shell.Current.GoToAsync("//main/order");
    }

    public void Sync()
    {
        Lines.Clear();
        foreach (var line in _cart.Lines)
            Lines.Add(line);
        Total = _cart.Total;

        var codes = _cart.Lines.Select(l => l.CurrencyCode).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (_cart.Lines.Count == 0)
            TotalSummary = "0";
        else if (codes.Count == 1)
            TotalSummary = $"{Total:0.##} {codes[0].ToUpperInvariant()}";
        else
            TotalSummary = $"{Total:0.##} (farklı para birimleri)";
    }
}
