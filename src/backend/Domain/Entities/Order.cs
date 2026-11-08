namespace StoreOps.Domain.Entities;

public sealed class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? FulfilledAtUtc { get; set; }
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}
