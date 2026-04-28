using System.Collections.ObjectModel;
using B2B.Contracts;
using B2B.Mobile.Core.Api;
using B2B.Mobile.Core.Shell;
using B2B.Mobile.Features.Notifications.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace B2B.Mobile.Features.Notifications.ViewModels;

public sealed partial class NotificationsViewModel : ObservableObject
{
    private readonly NotificationsService _svc;

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? error;
    [ObservableProperty] private string? apiTraceId;
    [ObservableProperty] private bool hasMore;

    public ObservableCollection<NotificationListItem> Items { get; } = new();

    private int _page = 1;
    private const int PageSize = 30;

    public NotificationsViewModel(NotificationsService svc) => _svc = svc;

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        Error = null;
        ApiTraceId = null;

        try
        {
            _page = 1;
            Items.Clear();
            await LoadNextPageInternalAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (IsBusy || !HasMore) return;
        IsBusy = true;
        Error = null;
        ApiTraceId = null;
        try
        {
            await LoadNextPageInternalAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadNextPageInternalAsync()
    {
        var resp = await _svc.GetInboxAsync(_page, PageSize, CancellationToken.None);
        if (!resp.Success || resp.Data is null)
        {
            Error = UserFacingApiMessage.Message(resp.Error, "Bildirimler yüklenemedi.");
            ApiTraceId = string.IsNullOrWhiteSpace(resp.TraceId) ? null : resp.TraceId;
            HasMore = false;
            return;
        }

        foreach (var it in resp.Data.Items)
            Items.Add(it);

        var meta = resp.Data.Meta;
        var loadedSoFar = meta.Page * meta.PageSize;
        HasMore = loadedSoFar < meta.Total && meta.Returned > 0;
        _page++;
    }

    [RelayCommand]
    private async Task OpenAsync(NotificationListItem? item)
    {
        if (item is null) return;
        if (!item.IsRead)
        {
            var resp = await _svc.MarkReadAsync(item.NotificationId, CancellationToken.None);
            if (resp.Success)
            {
                var idx = Items.IndexOf(item);
                if (idx >= 0)
                    Items[idx] = item with { IsRead = true };
            }
        }

        await Shell.Current.DisplayAlertAsync(item.Title, item.Body, "Tamam");
    }
}

