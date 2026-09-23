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

    // Required for optimistic concurrency; remove the property if you do not use it.
    [Timestamp]
    public byte[]? RowVersion { get; set; }
}
