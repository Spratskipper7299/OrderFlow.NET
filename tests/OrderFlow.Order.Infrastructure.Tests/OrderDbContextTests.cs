using Microsoft.EntityFrameworkCore;
using OrderFlow.Order.Domain.Orders;
using OrderFlow.Order.Infrastructure.Data;
using OrderFlow.Order.Infrastructure.Repositories;
using Xunit;

namespace OrderFlow.Order.Infrastructure.Tests;

public class OrderDbContextTests
{
    private static DbContextOptions<OrderDbContext> CreateInMemoryOptions()
    {
        return new DbContextOptionsBuilder<OrderDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
    }

    [Fact]
    public async Task PersistAndLoad_OrderWithLines_ShouldRetainState()
    {
        var options = CreateInMemoryOptions();
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var lines = new List<OrderLineRequest> { new(productId, 2, 49.99m) };

        var order = Domain.Orders.Order.Create(customerId, lines);
        order.MarkInventoryReserved();

        using (var context = new OrderDbContext(options))
        {
            var repo = new OrderCommandRepository(context);
            await repo.AddAsync(order);
            await repo.SaveChangesAsync();
        }

        using (var context = new OrderDbContext(options))
        {
            var repo = new OrderQueryRepository(context);
            var loadedOrder = await repo.GetByIdAsync(order.Id);

            Assert.NotNull(loadedOrder);
            Assert.Equal(order.Id, loadedOrder.Id);
            Assert.Equal(customerId, loadedOrder.CustomerId);
            Assert.Equal(OrderStatus.InventoryReserved, loadedOrder.Status);
            Assert.Single(loadedOrder.Lines);
            Assert.Equal(productId, loadedOrder.Lines[0].ProductId);
            Assert.Equal(2, loadedOrder.Lines[0].Quantity);
            Assert.Equal(49.99m, loadedOrder.Lines[0].UnitPrice);
            Assert.Equal(99.98m, loadedOrder.Total);
        }
    }

    [Fact]
    public void OrderDbContext_ShouldContainOrderAndOutboxEntities()
    {
        var options = CreateInMemoryOptions();
        using var context = new OrderDbContext(options);

        var entityTypes = context.Model.GetEntityTypes().Select(e => e.ClrType.Name).ToList();

        // Order-service business entities
        Assert.Contains("Order", entityTypes);
        Assert.Contains("OrderLine", entityTypes);

        // MassTransit transactional outbox/inbox entities
        Assert.Contains("OutboxMessage", entityTypes);
        Assert.Contains("OutboxState", entityTypes);
        Assert.Contains("InboxState", entityTypes);

        // Must NOT contain other services' entities
        Assert.DoesNotContain("Product", entityTypes);
        Assert.DoesNotContain("Payment", entityTypes);
    }
}
