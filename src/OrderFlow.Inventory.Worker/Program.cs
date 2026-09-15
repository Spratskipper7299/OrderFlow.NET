using OrderFlow.Inventory.Application;
using OrderFlow.Inventory.Infrastructure;
using OrderFlow.Inventory.Worker;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();
builder.Services.AddInventoryApplication();
builder.Services.AddInventoryInfrastructure(builder.Configuration, Assembly.GetExecutingAssembly());

var host = builder.Build();

if (builder.Environment.IsDevelopment())
{
    using var scope = host.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<OrderFlow.Inventory.Infrastructure.Data.InventoryDbContext>();
    dbContext.Database.Migrate();
}

host.Run();
