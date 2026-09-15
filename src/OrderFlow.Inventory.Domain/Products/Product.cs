using OrderFlow.Inventory.Domain.Exceptions;

namespace OrderFlow.Inventory.Domain.Products;

public sealed class Product
{
    public Guid Id { get; private set; }
    public string Sku { get; private set; }
    public string Name { get; private set; }
    public int AvailableQuantity { get; private set; }
    public int ReservedQuantity { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private Product(Guid id, string sku, string name, int availableQuantity)
    {
        Id = id;
        Sku = sku;
        Name = name;
        AvailableQuantity = availableQuantity;
        ReservedQuantity = 0;
    }

    public static Product Create(Guid id, string sku, string name, int availableQuantity)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            throw new DomainException("SKU cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Name cannot be empty.");
        }

        if (availableQuantity < 0)
        {
            throw new DomainException("Initial available quantity cannot be negative.");
        }

        return new Product(id, sku, name, availableQuantity);
    }

    public void Reserve(int quantity)
    {
        if (quantity <= 0)
        {
            throw new DomainException("Reserve quantity must be positive.");
        }

        if (AvailableQuantity < quantity)
        {
            throw new DomainException($"Insufficient available inventory for product {Id}. Requested: {quantity}, Available: {AvailableQuantity}");
        }

        AvailableQuantity -= quantity;
        ReservedQuantity += quantity;
    }

    public void Release(int quantity)
    {
        if (quantity <= 0)
        {
            throw new DomainException("Release quantity must be positive.");
        }

        if (ReservedQuantity < quantity)
        {
            throw new DomainException($"Cannot release more inventory than is reserved for product {Id}. Requested: {quantity}, Reserved: {ReservedQuantity}");
        }

        ReservedQuantity -= quantity;
        AvailableQuantity += quantity;
    }

    public void Commit(int quantity)
    {
        if (quantity <= 0)
        {
            throw new DomainException("Commit quantity must be positive.");
        }

        if (ReservedQuantity < quantity)
        {
            throw new DomainException($"Cannot commit more inventory than is reserved for product {Id}. Requested: {quantity}, Reserved: {ReservedQuantity}");
        }

        ReservedQuantity -= quantity;
    }
}
