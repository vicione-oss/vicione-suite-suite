using System.ComponentModel.DataAnnotations;
using Burger.Public.Contracts;
using MassTransit;

namespace Burger.Backend.StateMachines;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819: Properties should not return arrays", Justification = "DTO")]
public sealed class OrderBurgerState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }

    public int CurrentState { get; set; }

    public List<SuiteBurger> Burgers { get; } = [];

    // If using Optimistic concurrency, this property is required
    [Timestamp]
    public byte[]? RowVersion { get; set; }
}
