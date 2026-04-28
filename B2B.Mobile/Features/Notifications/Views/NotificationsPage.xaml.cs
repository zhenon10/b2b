using B2B.Mobile.Features.Notifications.ViewModels;

namespace B2B.Mobile.Features.Notifications.Views;

public partial class NotificationsPage : ContentPage
{
    public NotificationsPage(NotificationsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    private async void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (BindingContext is not NotificationsViewModel vm) return;
            var item = e.CurrentSelection?.FirstOrDefault() as B2B.Contracts.NotificationListItem;
            if (item is null) return;
            await vm.OpenCommand.ExecuteAsync(item);
        }
        finally
        {
            if (sender is CollectionView cv)
                cv.SelectedItem = null;
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is NotificationsViewModel vm && vm.Items.Count == 0)
            _ = vm.RefreshCommand.ExecuteAsync(null);
    }
}

