using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OrderFlow.Inventory.Infrastructure.Data;

public class InventoryDbContextFactory : IDesignTimeDbContextFactory<InventoryDbContext>
{
    public InventoryDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<InventoryDbContext>();
        optionsBuilder.UseSqlServer("Server=localhost,1433;Database=InventoryDb;User Id=sa;Password=OrderFlow_Dev_123;TrustServerCertificate=True;");

        return new InventoryDbContext(optionsBuilder.Options);
    }
}
