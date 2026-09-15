using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderFlow.Order.Application.Sagas;

namespace OrderFlow.Order.Infrastructure.Data.Configurations;

public class OrderSagaStateMap : IEntityTypeConfiguration<OrderSagaState>
{
    public void Configure(EntityTypeBuilder<OrderSagaState> entity)
    {
        entity.HasKey(x => x.CorrelationId);
        entity.Property(x => x.CurrentState).HasMaxLength(64);
        entity.Property(x => x.RowVersion).IsRowVersion();
    }
}
