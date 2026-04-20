namespace B2B.Contracts;

public sealed record PendingDealerDto(Guid UserId, string Email, string? DisplayName, DateTime CreatedAtUtc);
