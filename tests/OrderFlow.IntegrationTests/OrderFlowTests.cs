using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Contracts;
using OrderFlow.Order.Domain.Orders;
using OrderFlow.Order.Infrastructure.Data;
using OrderFlow.Inventory.Domain.Products;
using OrderFlow.Inventory.Infrastructure.Data;

namespace OrderFlow.IntegrationTests;

[Collection("Integration")]
public sealed class OrderFlowTests
{
    private readonly IntegrationTestFixture _fixture;

    public OrderFlowTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Create_order_and_reserve_inventory_confirms_order()
    {
        var productId = Guid.NewGuid();
        await SeedProductAsync(productId, "SKU-001", "Widget", availableQuantity: 50);

        var client = _fixture.ApiFactory.CreateClient();
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var request = new OrderFlow.Order.Application.Orders.Commands.CreateOrder.CreateOrderCommand(
            Guid.NewGuid(),
            new[] { new OrderFlow.Order.Application.Orders.Commands.CreateOrder.CreateOrderLine(productId, 5, 10m) }
        );

        var response = await client.PostAsJsonAsync("/api/orders", request);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        var orderId = Guid.Parse(json.RootElement.GetProperty("orderId").GetString()!);

        var order = await PollOrderStatusAsync(orderId, OrderStatus.Confirmed, timeout: TimeSpan.FromSeconds(30));
        Assert.Equal(OrderStatus.Confirmed, order!.Status);

        using (var scope = _fixture.InventoryWorkerHost!.Services.CreateScope())
        {
            var inventoryDb = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            var product = await inventoryDb.Products.FindAsync(productId);
            Assert.Equal(45, product!.AvailableQuantity);
            Assert.Equal(0, product.ReservedQuantity);
        }
    }

    [Fact]
    public async Task Insufficient_stock_rejects_order()
    {
        var productId = Guid.NewGuid();
        await SeedProductAsync(productId, "SKU-LOW", "LowStockItem", availableQuantity: 2);

        var client = _fixture.ApiFactory.CreateClient();
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var request = new OrderFlow.Order.Application.Orders.Commands.CreateOrder.CreateOrderCommand(
            Guid.NewGuid(),
            new[] { new OrderFlow.Order.Application.Orders.Commands.CreateOrder.CreateOrderLine(productId, 10, 5m) }
        );

        var response = await client.PostAsJsonAsync("/api/orders", request);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        var orderId = Guid.Parse(json.RootElement.GetProperty("orderId").GetString()!);

        var order = await PollOrderStatusAsync(orderId, OrderStatus.Rejected, timeout: TimeSpan.FromSeconds(30));
        Assert.Equal(OrderStatus.Rejected, order!.Status);
        Assert.Contains("Insufficient available inventory", order.RejectionReason);

        using (var scope = _fixture.InventoryWorkerHost!.Services.CreateScope())
        {
            var inventoryDb = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            var product = await inventoryDb.Products.FindAsync(productId);
            Assert.Equal(2, product!.AvailableQuantity);
        }
    }

    private async Task SeedProductAsync(Guid id, string sku, string name, int availableQuantity)
    {
        using var scope = _fixture.InventoryWorkerHost!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        db.Products.Add(Product.Create(id, sku, name, availableQuantity));
        await db.SaveChangesAsync();
    }

    private async Task<OrderFlow.Order.Domain.Orders.Order?> PollOrderStatusAsync(Guid orderId, OrderStatus expected, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var scope = _fixture.ApiFactory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
            var order = await db.Orders.AsNoTracking()
                .Include(o => o.Lines)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order?.Status == expected)
                return order;

            await Task.Delay(250);
        }

        using (var scope = _fixture.ApiFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
            return await db.Orders.AsNoTracking()
                .Include(o => o.Lines)
                .FirstOrDefaultAsync(o => o.Id == orderId);
        }
    }
}
