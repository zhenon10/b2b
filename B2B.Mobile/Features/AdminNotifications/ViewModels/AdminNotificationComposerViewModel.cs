using B2B.Mobile.Features.AdminNotifications.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace B2B.Mobile.Features.AdminNotifications.ViewModels;

public partial class AdminNotificationComposerViewModel : ObservableObject
{
    private readonly AdminNotificationsService _svc;

    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _body = "";
    [ObservableProperty] private string _dataJson = "";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private string? _lastResult;

    public AdminNotificationComposerViewModel(AdminNotificationsService svc) => _svc = svc;

    [RelayCommand]
    private async Task SendAsync(CancellationToken ct)
    {
        if (IsBusy) return;
        Error = null;
        LastResult = null;

        var t = (Title ?? "").Trim();
        var b = (Body ?? "").Trim();
        var d = (DataJson ?? "").Trim();

        if (string.IsNullOrWhiteSpace(t) || string.IsNullOrWhiteSpace(b))
        {
            Error = "Başlık ve mesaj zorunludur.";
            return;
        }

        IsBusy = true;
        try
        {
            var resp = await _svc.BroadcastAsync(t, b, d, ct);
            if (!resp.Success)
            {
                Error = resp.Error?.Message ?? "Bildirim gönderilemedi.";
                return;
            }

            LastResult = "Gönderildi.";
            Title = "";
            Body = "";
            DataJson = "";
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}

