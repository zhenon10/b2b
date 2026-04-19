using B2B.Mobile;
using B2B.Mobile.Core.Auth;
using B2B.Mobile.Features.Auth.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace B2B.Mobile.Features.Auth.Views;

public partial class RegisterPage : ContentPage
{
    public RegisterPage()
    {
        InitializeComponent();
        BindingContext = App.Services.GetRequiredService<RegisterViewModel>();
    }

    private async void OnGoLoginClicked(object? sender, EventArgs e)
    {
        await App.Services.GetRequiredService<ISessionSignOutHandler>().SignOutAndNavigateToLoginAsync();
    }
}

