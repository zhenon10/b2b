using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using B2B.Mobile.Core.Api;
using B2B.Mobile.Core;
using B2B.Mobile.Features.Auth.Services;

namespace B2B.Mobile.Features.Auth.ViewModels;

public partial class RegisterViewModel : ObservableObject
{
    private readonly AuthService _auth;
    private readonly CatalogNotifications _catalogEvents;

    public RegisterViewModel(AuthService auth, CatalogNotifications catalogEvents)
    {
        _auth = auth;
        _catalogEvents = catalogEvents;
    }

    [ObservableProperty] private string email = "";
    [ObservableProperty] private string password = "";
    [ObservableProperty] private string? displayName;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? error;
    [ObservableProperty] private string? apiTraceId;
    [ObservableProperty] private string? successMessage;

    [RelayCommand]
    private async Task RegisterAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        Error = null;
        ApiTraceId = null;
        SuccessMessage = null;

        try
        {
            var resp = await _auth.RegisterAsync(Email.Trim(), Password, DisplayName, CancellationToken.None);
            if (!resp.Success)
            {
                Error = UserFacingApiMessage.Message(resp.Error, "Kayıt başarısız.");
                ApiTraceId = string.IsNullOrWhiteSpace(resp.TraceId) ? null : resp.TraceId;
                return;
            }

            if (resp.Data is not null && string.IsNullOrWhiteSpace(resp.Data.AccessToken))
            {
                SuccessMessage = resp.Data.Message;
                return;
            }

            _catalogEvents.NotifySessionChanged();
            await Shell.Current.GoToAsync("//main/products");
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

