namespace B2B.Mobile.Features.Auth.Models;

public sealed record PendingDealerDto(Guid UserId, string Email, string? DisplayName, DateTime CreatedAtUtc);
