using B2B.Mobile.Features.Auth.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace B2B.Mobile.Features.Auth.Views;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsViewModel _vm;

    public SettingsPage()
    {
        InitializeComponent();
        _vm = App.Services.GetRequiredService<SettingsViewModel>();
        BindingContext = _vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.LoadDraftCommand.Execute(null);
    }
}
