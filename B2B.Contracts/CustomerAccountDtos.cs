using B2B.Domain.Enums;

namespace B2B.Contracts;

public sealed record CustomerAccountSummary(
    Guid SellerUserId,
    string? SellerDisplayName,
    string CurrencyCode,
    decimal Balance);

public sealed record CustomerAccountEntryDto(
    DateTime CreatedAtUtc,
    CustomerAccountEntryType Type,
    string CurrencyCode,
    decimal Amount,
    Guid? OrderId,
    long? OrderNumber);

