using OrderFlow.Inventory.Domain.Exceptions;
using OrderFlow.Inventory.Domain.Products;

namespace OrderFlow.Inventory.Domain.Tests;

public class ProductTests
{
    [Fact]
    public void Reserve_AvailableQuantity_Successfully()
    {
        var product = Product.Create(Guid.NewGuid(), "SKU1", "Name1", 10);

        product.Reserve(4);

        Assert.Equal(6, product.AvailableQuantity);
        Assert.Equal(4, product.ReservedQuantity);
    }

    [Fact]
    public void Reserve_MoreThanAvailable_ThrowsDomainException()
    {
        var product = Product.Create(Guid.NewGuid(), "SKU1", "Name1", 10);

        Assert.Throws<DomainException>(() => product.Reserve(11));
        
        Assert.Equal(10, product.AvailableQuantity);
        Assert.Equal(0, product.ReservedQuantity);
    }

    [Fact]
    public void Reserve_NegativeQuantity_ThrowsDomainException()
    {
        var product = Product.Create(Guid.NewGuid(), "SKU1", "Name1", 10);

        Assert.Throws<DomainException>(() => product.Reserve(-1));
    }

    [Fact]
    public void Reserve_ZeroQuantity_ThrowsDomainException()
    {
        var product = Product.Create(Guid.NewGuid(), "SKU1", "Name1", 10);

        Assert.Throws<DomainException>(() => product.Reserve(0));
    }

    [Fact]
    public void Release_ReservedQuantity_Successfully()
    {
        var product = Product.Create(Guid.NewGuid(), "SKU1", "Name1", 10);
        product.Reserve(4);

        product.Release(2);

        Assert.Equal(8, product.AvailableQuantity);
        Assert.Equal(2, product.ReservedQuantity);
    }

    [Fact]
    public void Release_MoreThanReserved_ThrowsDomainException()
    {
        var product = Product.Create(Guid.NewGuid(), "SKU1", "Name1", 10);
        product.Reserve(4);

        Assert.Throws<DomainException>(() => product.Release(5));
        
        Assert.Equal(6, product.AvailableQuantity);
        Assert.Equal(4, product.ReservedQuantity);
    }

    [Fact]
    public void Release_NegativeQuantity_ThrowsDomainException()
    {
        var product = Product.Create(Guid.NewGuid(), "SKU1", "Name1", 10);

        Assert.Throws<DomainException>(() => product.Release(-1));
    }

    [Fact]
    public void Release_ZeroQuantity_ThrowsDomainException()
    {
        var product = Product.Create(Guid.NewGuid(), "SKU1", "Name1", 10);

        Assert.Throws<DomainException>(() => product.Release(0));
    }

    [Fact]
    public void Commit_ReservedQuantity_Successfully()
    {
        var product = Product.Create(Guid.NewGuid(), "SKU1", "Name1", 10);
        product.Reserve(4);

        product.Commit(3);

        Assert.Equal(6, product.AvailableQuantity);
        Assert.Equal(1, product.ReservedQuantity);
    }

    [Fact]
    public void Commit_MoreThanReserved_ThrowsDomainException()
    {
        var product = Product.Create(Guid.NewGuid(), "SKU1", "Name1", 10);
        product.Reserve(4);

        Assert.Throws<DomainException>(() => product.Commit(5));
        
        Assert.Equal(6, product.AvailableQuantity);
        Assert.Equal(4, product.ReservedQuantity);
    }

    [Fact]
    public void Commit_NegativeQuantity_ThrowsDomainException()
    {
        var product = Product.Create(Guid.NewGuid(), "SKU1", "Name1", 10);

        Assert.Throws<DomainException>(() => product.Commit(-1));
    }

    [Fact]
    public void Commit_ZeroQuantity_ThrowsDomainException()
    {
        var product = Product.Create(Guid.NewGuid(), "SKU1", "Name1", 10);

        Assert.Throws<DomainException>(() => product.Commit(0));
    }
}
