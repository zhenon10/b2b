using B2B.Mobile.Features.AdminNotifications.ViewModels;

namespace B2B.Mobile.Features.AdminNotifications.Views;

public partial class AdminNotificationComposerPage : ContentPage
{
    public AdminNotificationComposerPage(AdminNotificationComposerViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}

