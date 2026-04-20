using System.ComponentModel;
using B2B.Mobile.Core.Connectivity;
using B2B.Mobile.Core.Finance;
using CommunityToolkit.Mvvm.ComponentModel;

namespace B2B.Mobile.Core.Shell;

public partial class MainHeaderViewModel : ObservableObject
{
    private readonly ExchangeRateService _rates;
    private readonly ConnectivityService _connectivity;

    public MainHeaderViewModel(ExchangeRateService rates, ConnectivityService connectivity)
    {
        _rates = rates;
        _connectivity = connectivity;
        _connectivity.PropertyChanged += OnConnectivityPropertyChanged;
        OnPropertyChanged(nameof(IsOffline));
        OnPropertyChanged(nameof(IsConstrainedNetwork));
        OnPropertyChanged(nameof(ShowConstrainedHint));
    }

    private void OnConnectivityPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ConnectivityService.IsOffline) or nameof(ConnectivityService.IsConstrained))
        {
            OnPropertyChanged(nameof(IsOffline));
            OnPropertyChanged(nameof(IsConstrainedNetwork));
            OnPropertyChanged(nameof(ShowConstrainedHint));
        }
    }

    [ObservableProperty] private string usdTryDisplay = "";

    public bool IsOffline => _connectivity.IsOffline;

    public bool IsConstrainedNetwork => _connectivity.IsConstrained;

    public bool ShowConstrainedHint => _connectivity.IsConstrained && !_connectivity.IsOffline;

    public async Task RefreshUsdTryAsync(CancellationToken ct = default)
    {
        UsdTryDisplay = "…";
        try
        {
            UsdTryDisplay = await _rates.GetUsdTryDisplayAsync(ct);
        }
        catch
        {
            UsdTryDisplay = "—";
        }
    }
}
