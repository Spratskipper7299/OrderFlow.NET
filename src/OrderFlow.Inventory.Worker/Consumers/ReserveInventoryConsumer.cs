using MassTransit;
using OrderFlow.Contracts;
using OrderFlow.Inventory.Application.Repositories;
using OrderFlow.Inventory.Domain.Exceptions;

namespace OrderFlow.Inventory.Worker.Consumers;

public class ReserveInventoryConsumer : IConsumer<ReserveInventory>
{
    private readonly IProductRepository _repository;

    public ReserveInventoryConsumer(IProductRepository repository)
    {
        _repository = repository;
    }

    public async Task Consume(ConsumeContext<ReserveInventory> context)
    {
        var command = context.Message;

        try
        {
            foreach (var item in command.Items)
            {
                var product = await _repository.GetByIdAsync(item.ProductId, context.CancellationToken);
                
                if (product == null)
                {
                    throw new DomainException($"Product with ID {item.ProductId} not found.");
                }

                product.Reserve(item.Quantity);
                await _repository.UpdateAsync(product, context.CancellationToken);
            }

            await context.Publish(new InventoryReserved(command.OrderId), context.CancellationToken);
            await _repository.SaveChangesAsync(context.CancellationToken);
        }
        catch (DomainException ex)
        {
            await context.Publish(new InventoryRejected(command.OrderId, ex.Message), context.CancellationToken);
        }
    }
}
