using OrderFlow.Payment.Application;
using OrderFlow.Payment.Infrastructure;
using OrderFlow.Payment.Worker;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

builder.Services.AddPaymentApplication();
builder.Services.AddPaymentInfrastructure(builder.Configuration, Assembly.GetExecutingAssembly());

var host = builder.Build();

if (builder.Environment.IsDevelopment())
{
    using var scope = host.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<OrderFlow.Payment.Infrastructure.Data.PaymentDbContext>();
    dbContext.Database.Migrate();
}

host.Run();
