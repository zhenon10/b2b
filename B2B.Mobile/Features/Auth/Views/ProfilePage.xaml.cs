using B2B.Mobile.Features.Auth.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace B2B.Mobile.Features.Auth.Views;

public partial class ProfilePage : ContentPage
{
    private readonly ProfileViewModel _vm;

    public ProfilePage()
    {
        InitializeComponent();
        _vm = App.Services.GetRequiredService<ProfileViewModel>();
        BindingContext = _vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = _vm.RefreshCommand.ExecuteAsync(null);
    }
}
