using B2B.Contracts;
using B2B.Mobile.Core.Api;

namespace B2B.Mobile.Features.Notifications.Services;

public sealed class NotificationsService
{
    private readonly ApiClient _api;

    public NotificationsService(ApiClient api) => _api = api;

    public Task<ApiResponse<PagedResult<NotificationListItem>>> GetInboxAsync(int page, int pageSize, CancellationToken ct) =>
        ApiTransientRetry.ExecuteAsync(
            () => _api.GetAsync<PagedResult<NotificationListItem>>($"/api/v1/notifications?page={page}&pageSize={pageSize}", ct),
            ct);

    public Task<ApiResponse<object>> MarkReadAsync(Guid notificationId, CancellationToken ct) =>
        _api.PostAsync<object>($"/api/v1/notifications/{notificationId}/read", ct);
}

