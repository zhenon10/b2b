using B2B.Mobile.Features.Orders.ViewModels;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;

namespace B2B.Mobile.Features.Orders.Views;

public partial class AdminOrdersPage : ContentPage
{
    private static readonly TimeSpan ListStaleAfter = TimeSpan.FromMinutes(2);

    private readonly AdminOrdersViewModel _vm;

    public Command<AdminOrdersViewModel.AdminOrderRowVm> CancelWithConfirmCommand { get; }

    public AdminOrdersPage()
    {
        InitializeComponent();
        _vm = App.Services.GetRequiredService<AdminOrdersViewModel>();
        BindingContext = _vm;
        _vm.PropertyChanged += VmOnPropertyChanged;

        CancelWithConfirmCommand = new Command<AdminOrdersViewModel.AdminOrderRowVm>(async item =>
        {
            if (item is null) return;
            if (!_vm.CanCancel(item)) return;

            var ok = await DisplayAlertAsync(
                "Sipariş iptali",
                $"Sipariş #{item.OrderNumber} iptal edilsin mi?",
                "İptal et",
                "Vazgeç");

            if (!ok) return;
            if (_vm.CancelOrderCommand.CanExecute(item))
                _vm.CancelOrderCommand.Execute(item);
        });
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            await _vm.EnsureFreshListAsync(ListStaleAfter);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            await DisplayAlertAsync("Sipariş onayları", "Sayfa yüklenirken bir sorun oluştu. Tekrar deneyin.", "Tamam");
        }
    }

    private async void VmOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AdminOrdersViewModel.HasSelectedDetail) && _vm.HasSelectedDetail)
        {
            try
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    if (AdminOrdersScroll is null || AdminOrderDetailCard is null)
                        return;
                    await AdminOrdersScroll.ScrollToAsync(AdminOrderDetailCard, ScrollToPosition.Start, animated: true);
                });
            }
            catch
            {
                // UX iyileştirmesi; başarısız olursa sessiz geç.
            }
        }

        if (e.PropertyName == nameof(AdminOrdersViewModel.ToastMessage) && !string.IsNullOrWhiteSpace(_vm.ToastMessage))
        {
            try
            {
                var msg = _vm.ToastMessage;
                _vm.ToastMessage = null;
                if (string.IsNullOrWhiteSpace(msg)) return;

                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    var toast = Toast.Make(msg, ToastDuration.Short, textSize: 14);
                    await toast.Show();
                });
            }
            catch
            {
                // Geri bildirim opsiyonel.
            }
        }
    }
}
