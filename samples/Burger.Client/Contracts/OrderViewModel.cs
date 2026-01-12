namespace Burger.Client.Contracts;

public enum OrderState
{
    None,
    Ordered,
    Delivered,
    Failed
}

public sealed class OrderViewModel
{
    public Guid OrderId { get; init; }
    public string? Description { get; set; }
    public DateTime OrderedWhen { get; } = DateTime.Now;
    public DateTime? DeliveredWhen { get; set; }
    public OrderState State { get; set; } = OrderState.None;
}
