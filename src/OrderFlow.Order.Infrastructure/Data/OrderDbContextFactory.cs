using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OrderFlow.Order.Infrastructure.Data;

public sealed class OrderDbContextFactory : IDesignTimeDbContextFactory<OrderDbContext>
{
    public OrderDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<OrderDbContext>();
        var connectionString = "Server=localhost,1433;Database=OrderDb;User Id=sa;Password=OrderFlow_Dev_123;TrustServerCertificate=True;";
            
        builder.UseSqlServer(connectionString);
        
        return new OrderDbContext(builder.Options);
    }
}
