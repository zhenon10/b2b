namespace B2B.Mobile.Core.Auth;

public interface IAuthSession
{
    Task<string?> GetAccessTokenAsync();
    Task SetAccessTokenAsync(string? token);
    Task ClearAsync();
}

