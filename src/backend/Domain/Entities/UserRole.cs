namespace StoreOps.Domain.Entities;

public sealed class UserRole
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string Role { get; set; } = string.Empty;
}
