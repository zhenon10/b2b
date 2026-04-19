using B2B.Mobile.Core.Finance;
using CommunityToolkit.Mvvm.ComponentModel;

namespace B2B.Mobile.Core.Shell;

public partial class MainHeaderViewModel : ObservableObject
{
    private readonly ExchangeRateService _rates;

    public MainHeaderViewModel(ExchangeRateService rates) =>
        _rates = rates;

    [ObservableProperty] private string usdTryDisplay = "";

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
