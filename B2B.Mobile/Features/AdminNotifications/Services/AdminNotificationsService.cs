using B2B.Contracts;
using B2B.Mobile.Core.Api;

namespace B2B.Mobile.Features.AdminNotifications.Services;

public sealed class AdminNotificationsService
{
    private readonly ApiClient _api;

    public AdminNotificationsService(ApiClient api) => _api = api;

    public Task<ApiResponse<object>> BroadcastAsync(string title, string body, string? dataJson, CancellationToken ct) =>
        _api.PostAsync<CreateAdminNotificationRequest, object>(
            "/api/v1/admin/notifications",
            new CreateAdminNotificationRequest(title, body, string.IsNullOrWhiteSpace(dataJson) ? null : dataJson.Trim()),
            ct);

    public Task<ApiResponse<object>> ClearAllAsync(CancellationToken ct) =>
        _api.DeleteAsync<object>("/api/v1/admin/notifications", ct);
}

