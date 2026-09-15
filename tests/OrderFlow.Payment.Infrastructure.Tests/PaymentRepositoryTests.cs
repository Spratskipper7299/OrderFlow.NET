using Microsoft.EntityFrameworkCore;
using OrderFlow.Payment.Domain.Payments;
using OrderFlow.Payment.Infrastructure.Data;
using OrderFlow.Payment.Infrastructure.Repositories;
using Xunit;

namespace OrderFlow.Payment.Infrastructure.Tests;

public class PaymentRepositoryTests
{
    private static DbContextOptions<PaymentDbContext> CreateInMemoryOptions()
    {
        return new DbContextOptionsBuilder<PaymentDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
    }

    [Fact]
    public async Task AddAndGet_ShouldPersistPayment()
    {
        var options = CreateInMemoryOptions();
        var id = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var payment = PaymentRecord.Create(id, orderId, 250.75m);

        using (var context = new PaymentDbContext(options))
        {
            var repo = new PaymentRepository(context);
            await repo.AddAsync(payment);
            await repo.SaveChangesAsync();
        }

        using (var context = new PaymentDbContext(options))
        {
            var repo = new PaymentRepository(context);
            var loaded = await repo.GetByIdAsync(id);

            Assert.NotNull(loaded);
            Assert.Equal(id, loaded.Id);
            Assert.Equal(orderId, loaded.OrderId);
            Assert.Equal(250.75m, loaded.Amount);
            Assert.Equal(PaymentStatus.Pending, loaded.Status);
        }
    }

    [Fact]
    public async Task GetByOrderId_ShouldReturnPayment()
    {
        var options = CreateInMemoryOptions();
        var id = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var payment = PaymentRecord.Create(id, orderId, 100m);

        using (var context = new PaymentDbContext(options))
        {
            var repo = new PaymentRepository(context);
            await repo.AddAsync(payment);
            await repo.SaveChangesAsync();
        }

        using (var context = new PaymentDbContext(options))
        {
            var repo = new PaymentRepository(context);
            var loaded = await repo.GetByOrderIdAsync(orderId);

            Assert.NotNull(loaded);
            Assert.Equal(id, loaded.Id);
            Assert.Equal(orderId, loaded.OrderId);
        }
    }

    [Fact]
    public async Task Update_ShouldPersistStatusChange()
    {
        var options = CreateInMemoryOptions();
        var id = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var payment = PaymentRecord.Create(id, orderId, 100m);

        using (var context = new PaymentDbContext(options))
        {
            var repo = new PaymentRepository(context);
            await repo.AddAsync(payment);
            await repo.SaveChangesAsync();
        }

        using (var context = new PaymentDbContext(options))
        {
            var repo = new PaymentRepository(context);
            var loaded = await repo.GetByIdAsync(id);
            loaded!.Authorize();
            
            await repo.UpdateAsync(loaded);
            await repo.SaveChangesAsync();
        }

        using (var context = new PaymentDbContext(options))
        {
            var repo = new PaymentRepository(context);
            var loaded = await repo.GetByIdAsync(id);
            
            Assert.NotNull(loaded);
            Assert.Equal(PaymentStatus.Authorized, loaded.Status);
        }
    }

    [Fact]
    public void PaymentDbContext_ShouldOnlyContainPaymentAndOutboxEntities()
    {
        var options = CreateInMemoryOptions();
        using var context = new PaymentDbContext(options);

        var entityTypes = context.Model.GetEntityTypes().Select(e => e.ClrType.Name).ToList();

        Assert.Contains("PaymentRecord", entityTypes);
        Assert.Contains("InboxState", entityTypes);
        Assert.Contains("OutboxMessage", entityTypes);
        Assert.Contains("OutboxState", entityTypes);
        Assert.Equal(4, entityTypes.Count);
    }
}
