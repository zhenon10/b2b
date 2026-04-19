using System.Collections.ObjectModel;
using B2B.Mobile.Features.Auth.Models;
using B2B.Mobile.Features.Auth.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace B2B.Mobile.Features.Auth.ViewModels;

public partial class PendingDealersViewModel : ObservableObject
{
    private readonly AdminUsersService _adminUsers;

    public ObservableCollection<PendingDealerDto> Items { get; } = new();

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? error;

    public PendingDealersViewModel(AdminUsersService adminUsers) => _adminUsers = adminUsers;

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsBusy) return;
        await RefreshCoreAsync();
    }

    /// <summary>Yenileme; zaten <see cref="IsBusy"/> ise dışarıdan çağrılabilir (onay sonrası vb.).</summary>
    private async Task RefreshCoreAsync()
    {
        IsBusy = true;
        Error = null;
        try
        {
            var resp = await _adminUsers.GetPendingDealersAsync(CancellationToken.None);
            if (!resp.Success || resp.Data is null)
            {
                Error = resp.Error?.Message ?? "Liste yüklenemedi.";
                Items.Clear();
                return;
            }

            Items.Clear();
            foreach (var row in resp.Data.OrderBy(x => x.CreatedAtUtc))
                Items.Add(row);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ApproveAsync(PendingDealerDto? item)
    {
        if (item is null || IsBusy) return;

        var ok = await Shell.Current.DisplayAlertAsync(
            "Üyeliği onayla",
            $"{item.Email} adresli bayi hesabını onaylamak istiyor musunuz?",
            "Onayla",
            "İptal");
        if (!ok) return;

        IsBusy = true;
        Error = null;
        try
        {
            var resp = await _adminUsers.ApproveDealerAsync(item.UserId, CancellationToken.None);
            if (!resp.Success)
            {
                Error = resp.Error?.Message ?? "Onay başarısız.";
                return;
            }

            await RefreshCoreAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }
}
