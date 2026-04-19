namespace B2B.Domain.Entities;

public sealed class Role
{
    public Guid RoleId { get; set; }

    public string Name { get; set; } = null!;
    public string NormalizedName { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}

