using B2B.Contracts;
using B2B.Mobile.Core.Api;

namespace B2B.Mobile.Core.Push;

public sealed class PushTokensService
{
    private readonly ApiClient _api;

    public PushTokensService(ApiClient api) => _api = api;

    public Task<ApiResponse<RegisterPushTokenResponse>> RegisterAsync(string token, string platform, CancellationToken ct = default) =>
        _api.PostAsync<RegisterPushTokenRequest, RegisterPushTokenResponse>(
            "api/v1/push-tokens",
            new RegisterPushTokenRequest(token, platform),
            ct);
}

