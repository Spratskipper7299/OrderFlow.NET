using MassTransit;
using Moq;
using OrderFlow.Contracts;
using OrderFlow.Inventory.Application.Repositories;
using OrderFlow.Inventory.Domain.Products;
using OrderFlow.Inventory.Worker.Consumers;
using Xunit;

namespace OrderFlow.Inventory.Infrastructure.Tests;

public class ReserveInventoryConsumerTests
{
    [Fact]
    public async Task Consume_SuccessfulReservation_PublishesInventoryReserved()
    {
        var repoMock = new Mock<IProductRepository>();
        var product = Product.Create(Guid.NewGuid(), "SKU1", "Name", 100);
        repoMock.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(product);

        var contextMock = new Mock<ConsumeContext<ReserveInventory>>();
        var orderId = Guid.NewGuid();
        contextMock.Setup(x => x.Message)
                   .Returns(new ReserveInventory(orderId, new List<OrderItemDto> { new OrderItemDto(product.Id, 10) }));

        var consumer = new ReserveInventoryConsumer(repoMock.Object);

        await consumer.Consume(contextMock.Object);

        contextMock.Verify(x => x.Publish(
            It.Is<InventoryReserved>(msg => msg.OrderId == orderId),
            It.IsAny<CancellationToken>()), Times.Once);

        repoMock.Verify(x => x.UpdateAsync(product, It.IsAny<CancellationToken>()), Times.Once);
        repoMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        
        Assert.Equal(90, product.AvailableQuantity);
        Assert.Equal(10, product.ReservedQuantity);
    }

    [Fact]
    public async Task Consume_InsufficientInventory_PublishesInventoryRejected()
    {
        var repoMock = new Mock<IProductRepository>();
        var product = Product.Create(Guid.NewGuid(), "SKU1", "Name", 5);
        repoMock.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(product);

        var contextMock = new Mock<ConsumeContext<ReserveInventory>>();
        var orderId = Guid.NewGuid();
        contextMock.Setup(x => x.Message)
                   .Returns(new ReserveInventory(orderId, new List<OrderItemDto> { new OrderItemDto(product.Id, 10) }));

        var consumer = new ReserveInventoryConsumer(repoMock.Object);

        await consumer.Consume(contextMock.Object);

        contextMock.Verify(x => x.Publish(
            It.Is<InventoryRejected>(msg => msg.OrderId == orderId && msg.Reason.Contains("Insufficient available inventory")),
            It.IsAny<CancellationToken>()), Times.Once);

        repoMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        
        Assert.Equal(5, product.AvailableQuantity);
        Assert.Equal(0, product.ReservedQuantity);
    }

    [Fact]
    public async Task Consume_MissingProduct_PublishesInventoryRejected()
    {
        var repoMock = new Mock<IProductRepository>();
        repoMock.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Product?)null);

        var contextMock = new Mock<ConsumeContext<ReserveInventory>>();
        var orderId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        contextMock.Setup(x => x.Message)
                   .Returns(new ReserveInventory(orderId, new List<OrderItemDto> { new OrderItemDto(productId, 10) }));

        var consumer = new ReserveInventoryConsumer(repoMock.Object);

        await consumer.Consume(contextMock.Object);

        contextMock.Verify(x => x.Publish(
            It.Is<InventoryRejected>(msg => msg.OrderId == orderId && msg.Reason.Contains("not found")),
            It.IsAny<CancellationToken>()), Times.Once);

        repoMock.Verify(x => x.UpdateAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Never);
        repoMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
