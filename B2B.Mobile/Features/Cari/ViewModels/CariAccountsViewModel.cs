using System.Collections.ObjectModel;
using B2B.Contracts;
using B2B.Mobile.Core.Api;
using B2B.Mobile.Features.Cari.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace B2B.Mobile.Features.Cari.ViewModels;

public sealed partial class CariAccountsViewModel : ObservableObject
{
    private readonly CariService _svc;

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? error;
    [ObservableProperty] private string? apiTraceId;

    public ObservableCollection<CustomerAccountSummary> Items { get; } = new();

    public CariAccountsViewModel(CariService svc) => _svc = svc;

    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        Error = null;
        ApiTraceId = null;

        try
        {
            Items.Clear();
            var resp = await _svc.ListAsync(CancellationToken.None);
            if (!resp.Success || resp.Data is null)
            {
                Error = UserFacingApiMessage.Message(resp.Error, "Cari hesaplar yüklenemedi.");
                ApiTraceId = string.IsNullOrWhiteSpace(resp.TraceId) ? null : resp.TraceId;
                return;
            }

            foreach (var row in resp.Data)
                Items.Add(row);
        }
        finally
        {
            IsBusy = false;
        }
    }
}

