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
    public DateTimeOffset OrderedWhen { get; } = DateTime.Now;
    public DateTimeOffset? DeliveredWhen { get; set; }
    public OrderState State { get; set; } = OrderState.None;
}
