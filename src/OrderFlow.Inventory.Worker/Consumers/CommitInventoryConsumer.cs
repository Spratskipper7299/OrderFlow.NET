using MassTransit;
using OrderFlow.Contracts;
using OrderFlow.Inventory.Application.Repositories;
using OrderFlow.Inventory.Domain.Exceptions;

namespace OrderFlow.Inventory.Worker.Consumers;

public class CommitInventoryConsumer : IConsumer<CommitInventory>
{
    private readonly IProductRepository _repository;

    public CommitInventoryConsumer(IProductRepository repository)
    {
        _repository = repository;
    }

    public async Task Consume(ConsumeContext<CommitInventory> context)
    {
        var command = context.Message;

        foreach (var item in command.Items)
        {
            var product = await _repository.GetByIdAsync(item.ProductId, context.CancellationToken);
            if (product == null)
            {
                throw new DomainException($"Product with ID {item.ProductId} not found.");
            }

            product.Commit(item.Quantity);
            await _repository.UpdateAsync(product, context.CancellationToken);
        }

        await context.Publish(new InventoryCommitted(command.OrderId), context.CancellationToken);
        await _repository.SaveChangesAsync(context.CancellationToken);
    }
}
