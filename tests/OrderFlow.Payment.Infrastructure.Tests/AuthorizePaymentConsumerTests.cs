using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using OrderFlow.Contracts;
using OrderFlow.Payment.Application;
using OrderFlow.Payment.Application.Commands;
using OrderFlow.Payment.Application.Repositories;
using OrderFlow.Payment.Domain.Exceptions;
using OrderFlow.Payment.Domain.Payments;
using OrderFlow.Payment.Infrastructure.Data;
using OrderFlow.Payment.Worker.Consumers;
using System.Reflection;

namespace OrderFlow.Payment.Infrastructure.Tests;

public class AuthorizePaymentConsumerTests
{
    [Fact]
    public async Task Consume_SuccessfulAuthorization_PublishesPaymentAuthorizedAndPersistsState()
    {
        var mediatorMock = new Mock<IMediator>();
        var repoMock = new Mock<IPaymentRepository>();

        var orderId = Guid.NewGuid();
        var command = new AuthorizePayment(orderId, 100.00m, "token-123");

        var contextMock = new Mock<ConsumeContext<AuthorizePayment>>();
        contextMock.Setup(x => x.Message).Returns(command);

        var consumer = new AuthorizePaymentConsumer(mediatorMock.Object, repoMock.Object);

        await consumer.Consume(contextMock.Object);

        mediatorMock.Verify(x => x.Send(
            It.Is<AuthorizePaymentCommand>(cmd => cmd.OrderId == orderId && cmd.Amount == 100.00m && cmd.PaymentToken == "token-123"),
            It.IsAny<CancellationToken>()), Times.Once);

        contextMock.Verify(x => x.Publish(
            It.Is<PaymentAuthorized>(msg => msg.OrderId == orderId && msg.TransactionId == "token-123"),
            It.IsAny<CancellationToken>()), Times.Once);

        repoMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Consume_BusinessRejection_PublishesPaymentRejected()
    {
        var mediatorMock = new Mock<IMediator>();
        var repoMock = new Mock<IPaymentRepository>();

        mediatorMock.Setup(x => x.Send(It.IsAny<AuthorizePaymentCommand>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new DomainException("Payment amount must be greater than zero."));

        var orderId = Guid.NewGuid();
        var command = new AuthorizePayment(orderId, -10.00m, "token-invalid");

        var contextMock = new Mock<ConsumeContext<AuthorizePayment>>();
        contextMock.Setup(x => x.Message).Returns(command);

        var consumer = new AuthorizePaymentConsumer(mediatorMock.Object, repoMock.Object);

        await consumer.Consume(contextMock.Object);

        contextMock.Verify(x => x.Publish(
            It.Is<PaymentRejected>(msg => msg.OrderId == orderId && msg.Reason.Contains("greater than zero")),
            It.IsAny<CancellationToken>()), Times.Once);

        repoMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Consume_ExistingPaymentCannotBeAuthorizedTwice_PublishesPaymentRejected()
    {
        var dbOptions = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var dbContext = new PaymentDbContext(dbOptions);
        var repository = new Repositories.PaymentRepository(dbContext);

        // Pre-create an authorized payment
        var orderId = Guid.NewGuid();
        var payment = PaymentRecord.Create(Guid.NewGuid(), orderId, 50.00m);
        payment.Authorize();
        await repository.AddAsync(payment);
        await repository.SaveChangesAsync();

        var mediatorMock = new Mock<IMediator>();
        mediatorMock.Setup(x => x.Send(It.IsAny<AuthorizePaymentCommand>(), It.IsAny<CancellationToken>()))
                    .Returns<AuthorizePaymentCommand, CancellationToken>(async (cmd, ct) =>
                    {
                        var existing = await repository.GetByOrderIdAsync(cmd.OrderId, ct);
                        existing!.Authorize(); // Will throw DomainException: Payment is already authorized.
                        await repository.UpdateAsync(existing, ct);
                        await repository.SaveChangesAsync(ct);
                    });

        var contextMock = new Mock<ConsumeContext<AuthorizePayment>>();
        contextMock.Setup(x => x.Message).Returns(new AuthorizePayment(orderId, 50.00m, "token-dup"));

        var consumer = new AuthorizePaymentConsumer(mediatorMock.Object, repository);

        await consumer.Consume(contextMock.Object);

        contextMock.Verify(x => x.Publish(
            It.Is<PaymentRejected>(msg => msg.OrderId == orderId && msg.Reason.Contains("already authorized")),
            It.IsAny<CancellationToken>()), Times.Once);

        // Verify status remains Authorized in DB
        var reloaded = await repository.GetByOrderIdAsync(orderId);
        Assert.NotNull(reloaded);
        Assert.Equal(PaymentStatus.Authorized, reloaded.Status);
    }

    [Fact]
    public void ConsumerDependencies_CanBeResolvedByDI()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PaymentDb"] = "Server=localhost;Database=PaymentDbTest;Trusted_Connection=True;"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddPaymentApplication();
        services.AddPaymentInfrastructure(configuration, typeof(AuthorizePaymentConsumer).Assembly);

        var serviceProvider = services.BuildServiceProvider();

        using var scope = serviceProvider.CreateScope();
        var consumer = scope.ServiceProvider.GetService<AuthorizePaymentConsumer>();
        
        Assert.NotNull(consumer);
    }
}
