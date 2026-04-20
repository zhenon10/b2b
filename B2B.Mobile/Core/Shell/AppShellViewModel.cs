using System.Collections.Specialized;
using B2B.Mobile.Features.Cart.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace B2B.Mobile.Core.Shell;

public partial class AppShellViewModel : ObservableObject
{
    private readonly CartService _cart;

    [ObservableProperty] private string? cartBadgeText;
    [ObservableProperty] private string cartTabTitle = "Sepet";

    public AppShellViewModel(CartService cart)
    {
        _cart = cart;
        if (_cart.Lines is INotifyCollectionChanged n)
            n.CollectionChanged += (_, _) => MainThread.BeginInvokeOnMainThread(SyncCartBadge);
        SyncCartBadge();
    }

    private void SyncCartBadge()
    {
        var qty = _cart.Lines.Sum(l => l.Quantity);
        CartBadgeText = qty <= 0 ? null : qty.ToString();
        CartTabTitle = qty <= 0 ? "Sepet" : $"Sepet ({qty})";
    }
}

