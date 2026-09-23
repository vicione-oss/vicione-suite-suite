using Burger.Backend.StateMachines;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sdk.Backend.Extensions;

namespace Burger.Backend.DbContext;

public sealed class OrderBurgerClassMap : SagaClassMap<OrderBurgerState>
{
    protected override void Configure(EntityTypeBuilder<OrderBurgerState> entity, ModelBuilder model)
    {
        entity.Property(e => e.OrderId);
        entity.Property(e => e.CurrentState);
        entity.Property(e => e.Burgers).PersistAsJson();

        // Required for optimistic concurrency; remove the property if you do not use it.
        entity.Property(x => x.RowVersion).IsRowVersion();
    }
}
