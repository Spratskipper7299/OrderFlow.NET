using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace OrderFlow.Order.Infrastructure.Data;

public sealed class OrderDbContext : DbContext
{
    public OrderDbContext(DbContextOptions<OrderDbContext> options)
        : base(options)
    {
    }

    public DbSet<Domain.Orders.Order> Orders => Set<Domain.Orders.Order>();
    public DbSet<Application.Sagas.OrderSagaState> SagaStates => Set<Application.Sagas.OrderSagaState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrderDbContext).Assembly);

        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
