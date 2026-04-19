using System.Text.Json;

namespace B2B.Mobile.Core.Auth;

public static class JwtRoleReader
{
    public static bool IsAdmin(string? token) =>
        ReadRoles(token).Contains("Admin", StringComparer.OrdinalIgnoreCase);

    public static HashSet<string> ReadRoles(string? token)
    {
        var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(token)) return roles;

        var parts = token.Split('.');
        if (parts.Length < 2) return roles;

        try
        {
            var payloadJson = Base64UrlDecodeToString(parts[1]);
            using var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;

            var keys = new[]
            {
                "role",
                "roles",
                "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
            };

            foreach (var key in keys)
            {
                if (!root.TryGetProperty(key, out var el)) continue;

                if (el.ValueKind == JsonValueKind.String)
                {
                    var r = el.GetString();
                    if (!string.IsNullOrWhiteSpace(r)) roles.Add(r);
                }
                else if (el.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in el.EnumerateArray())
                    {
                        if (item.ValueKind != JsonValueKind.String) continue;
                        var r = item.GetString();
                        if (!string.IsNullOrWhiteSpace(r)) roles.Add(r);
                    }
                }
            }
        }
        catch
        {
            // ignore malformed token
        }

        return roles;
    }

    private static string Base64UrlDecodeToString(string base64Url)
    {
        var s = base64Url.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }
        var bytes = Convert.FromBase64String(s);
        return System.Text.Encoding.UTF8.GetString(bytes);
    }
}
