using Microsoft.EntityFrameworkCore;
using OrderFlow.Inventory.Domain.Products;
using OrderFlow.Inventory.Infrastructure.Data;
using OrderFlow.Inventory.Infrastructure.Repositories;
using Xunit;

namespace OrderFlow.Inventory.Infrastructure.Tests;

public class ProductRepositoryTests
{
    private static DbContextOptions<InventoryDbContext> CreateInMemoryOptions()
    {
        return new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
    }

    [Fact]
    public async Task PersistAndLoad_Product_ShouldRetainState()
    {
        var options = CreateInMemoryOptions();
        var productId = Guid.NewGuid();
        var product = Product.Create(productId, "SKU1", "Test Product", 100);

        using (var context = new InventoryDbContext(options))
        {
            var repo = new ProductRepository(context);
            context.Products.Add(product);
            await context.SaveChangesAsync();
        }

        using (var context = new InventoryDbContext(options))
        {
            var repo = new ProductRepository(context);
            var loadedProduct = await repo.GetByIdAsync(productId);

            Assert.NotNull(loadedProduct);
            Assert.Equal(productId, loadedProduct.Id);
            Assert.Equal("SKU1", loadedProduct.Sku);
            Assert.Equal("Test Product", loadedProduct.Name);
            Assert.Equal(100, loadedProduct.AvailableQuantity);
            Assert.Equal(0, loadedProduct.ReservedQuantity);
        }
    }

    [Fact]
    public async Task Reserve_ShouldBePersistedCorrectly()
    {
        var options = CreateInMemoryOptions();
        var productId = Guid.NewGuid();
        var product = Product.Create(productId, "SKU1", "Test Product", 100);

        using (var context = new InventoryDbContext(options))
        {
            context.Products.Add(product);
            await context.SaveChangesAsync();
        }

        using (var context = new InventoryDbContext(options))
        {
            var repo = new ProductRepository(context);
            var loadedProduct = await repo.GetByIdAsync(productId);
            loadedProduct!.Reserve(20);
            await repo.UpdateAsync(loadedProduct);
            await repo.SaveChangesAsync();
        }

        using (var context = new InventoryDbContext(options))
        {
            var repo = new ProductRepository(context);
            var loadedProduct = await repo.GetByIdAsync(productId);
            
            Assert.Equal(80, loadedProduct!.AvailableQuantity);
            Assert.Equal(20, loadedProduct.ReservedQuantity);
        }
    }

    [Fact]
    public async Task Release_ShouldBePersistedCorrectly()
    {
        var options = CreateInMemoryOptions();
        var productId = Guid.NewGuid();
        var product = Product.Create(productId, "SKU1", "Test Product", 100);
        product.Reserve(20);

        using (var context = new InventoryDbContext(options))
        {
            context.Products.Add(product);
            await context.SaveChangesAsync();
        }

        using (var context = new InventoryDbContext(options))
        {
            var repo = new ProductRepository(context);
            var loadedProduct = await repo.GetByIdAsync(productId);
            loadedProduct!.Release(5);
            await repo.UpdateAsync(loadedProduct);
            await repo.SaveChangesAsync();
        }

        using (var context = new InventoryDbContext(options))
        {
            var repo = new ProductRepository(context);
            var loadedProduct = await repo.GetByIdAsync(productId);
            
            Assert.Equal(85, loadedProduct!.AvailableQuantity);
            Assert.Equal(15, loadedProduct.ReservedQuantity);
        }
    }

    [Fact]
    public async Task Commit_ShouldBePersistedCorrectly()
    {
        var options = CreateInMemoryOptions();
        var productId = Guid.NewGuid();
        var product = Product.Create(productId, "SKU1", "Test Product", 100);
        product.Reserve(20);

        using (var context = new InventoryDbContext(options))
        {
            context.Products.Add(product);
            await context.SaveChangesAsync();
        }

        using (var context = new InventoryDbContext(options))
        {
            var repo = new ProductRepository(context);
            var loadedProduct = await repo.GetByIdAsync(productId);
            loadedProduct!.Commit(5);
            await repo.UpdateAsync(loadedProduct);
            await repo.SaveChangesAsync();
        }

        using (var context = new InventoryDbContext(options))
        {
            var repo = new ProductRepository(context);
            var loadedProduct = await repo.GetByIdAsync(productId);
            
            Assert.Equal(80, loadedProduct!.AvailableQuantity);
            Assert.Equal(15, loadedProduct.ReservedQuantity);
        }
    }

    [Fact]
    public void InventoryDbContext_ShouldOnlyContainInventoryEntities()
    {
        var options = CreateInMemoryOptions();
        using var context = new InventoryDbContext(options);

        var entityTypes = context.Model.GetEntityTypes().Select(e => e.ClrType.Name).ToList();

        Assert.Contains("Product", entityTypes);
        Assert.Contains("InboxState", entityTypes);
        Assert.Contains("OutboxMessage", entityTypes);
        Assert.Contains("OutboxState", entityTypes);
        Assert.Equal(4, entityTypes.Count);
    }
}
