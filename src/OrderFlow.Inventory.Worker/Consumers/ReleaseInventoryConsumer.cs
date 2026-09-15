using MassTransit;
using OrderFlow.Contracts;
using OrderFlow.Inventory.Application.Repositories;
using OrderFlow.Inventory.Domain.Exceptions;

namespace OrderFlow.Inventory.Worker.Consumers;

public class ReleaseInventoryConsumer : IConsumer<ReleaseInventory>
{
    private readonly IProductRepository _repository;

    public ReleaseInventoryConsumer(IProductRepository repository)
    {
        _repository = repository;
    }

    public async Task Consume(ConsumeContext<ReleaseInventory> context)
    {
        var command = context.Message;

        foreach (var item in command.Items)
        {
            var product = await _repository.GetByIdAsync(item.ProductId, context.CancellationToken);
            if (product == null)
            {
                throw new DomainException($"Product with ID {item.ProductId} not found.");
            }

            product.Release(item.Quantity);
            await _repository.UpdateAsync(product, context.CancellationToken);
        }

        await context.Publish(new InventoryReleased(command.OrderId), context.CancellationToken);
        await _repository.SaveChangesAsync(context.CancellationToken);
    }
}
