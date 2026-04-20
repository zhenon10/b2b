namespace B2B.Domain.Entities;

public sealed class User
{
    public Guid UserId { get; set; }

    public string Email { get; set; } = null!;
    public string NormalizedEmail { get; set; } = null!;
    public string? DisplayName { get; set; }

    public byte[] PasswordHash { get; set; } = null!;
    public byte[]? PasswordSalt { get; set; }

    public bool IsActive { get; set; }

    /// <summary>When set, the user may sign in. Self-service Dealer registration leaves this null until an admin approves.</summary>
    public DateTime? ApprovedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public byte[] RowVer { get; set; } = null!;

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();

    public ICollection<Product> ProductsAsSeller { get; set; } = new List<Product>();
    public ICollection<Order> OrdersAsBuyer { get; set; } = new List<Order>();
    public ICollection<Order> OrdersAsSeller { get; set; } = new List<Order>();

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}

