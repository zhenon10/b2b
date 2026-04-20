namespace B2B.Contracts;

public sealed record RegisterRequest(string Email, string Password, string? DisplayName);
public sealed record LoginRequest(string Email, string Password);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public sealed record ProfileResponse(
    Guid UserId,
    string Email,
    string? DisplayName,
    IReadOnlyList<string> Roles,
    DateTime? ApprovedAtUtc);
public sealed record AuthResponse(string AccessToken, string RefreshToken);
public sealed record RegisterResponse(string? AccessToken, string Message, string? RefreshToken);
public sealed record RefreshRequest(string RefreshToken);
