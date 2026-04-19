using B2B.Mobile.Features.Auth.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace B2B.Mobile.Features.Auth.Views;

public partial class AdminHubPage : ContentPage
{
    public AdminHubPage()
    {
        InitializeComponent();
        BindingContext = App.Services.GetRequiredService<AdminHubViewModel>();
    }
}
