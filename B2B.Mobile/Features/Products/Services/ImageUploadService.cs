using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using B2B.Mobile.Core.Api;
using B2B.Mobile.Core.Auth;

namespace B2B.Mobile.Features.Products.Services;

public sealed class ImageUploadService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IAuthSession _session;
    private readonly ISessionSignOutHandler _signOut;

    public ImageUploadService(
        IHttpClientFactory httpFactory,
        IAuthSession session,
        ISessionSignOutHandler signOut)
    {
        _httpFactory = httpFactory;
        _session = session;
        _signOut = signOut;
    }

    private sealed record UploadImageResponse(string Url);

    public async Task<ApiResponse<string>> UploadProductImageAsync(FileResult file, CancellationToken ct)
    {
        var http = _httpFactory.CreateClient("api");

        var token = await _session.GetAccessTokenAsync();
        if (!string.IsNullOrWhiteSpace(token))
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        await using var stream = await file.OpenReadAsync();
        using var content = new MultipartFormDataContent();

        var streamContent = new StreamContent(stream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType ?? "application/octet-stream");
        content.Add(streamContent, "file", file.FileName);

        using var resp = await http.PostAsync("/api/v1/uploads/images", content, ct);
        if (resp.StatusCode == HttpStatusCode.Unauthorized)
        {
            try
            {
                await _signOut.SignOutAndNavigateToLoginAsync(ct, LoginSessionEndKind.SessionExpired);
            }
            catch
            {
                // ignored
            }

            return new ApiResponse<string>(false, null, new ApiError("unauthorized", "Oturum geçersiz veya süresi doldu.", null), TraceId: "");
        }

        if (resp.StatusCode == HttpStatusCode.Forbidden)
        {
            return new ApiResponse<string>(false, null, new ApiError("forbidden", "Bu işlem için yetkiniz yok.", null), TraceId: "");
        }

        var payload = await resp.Content.ReadFromJsonAsync<ApiResponse<UploadImageResponse>>(cancellationToken: ct)
                      ?? new ApiResponse<UploadImageResponse>(false, null, new ApiError("invalid_response", "Empty response.", null), TraceId: "");

        if (!payload.Success
            && payload.Error is { Code: var code }
            && string.Equals(code, "unauthorized", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                await _signOut.SignOutAndNavigateToLoginAsync(ct, LoginSessionEndKind.SessionExpired);
            }
            catch
            {
                // ignored
            }
        }

        if (!payload.Success || payload.Data is null)
            return new ApiResponse<string>(false, null, payload.Error, payload.TraceId);

        return new ApiResponse<string>(true, payload.Data.Url, null, payload.TraceId);
    }
}

