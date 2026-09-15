using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OrderFlow.Payment.Infrastructure.Data;

public class PaymentDbContextFactory : IDesignTimeDbContextFactory<PaymentDbContext>
{
    public PaymentDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PaymentDbContext>();
        
        // This is only used for EF Core tools (migrations) at design time
        optionsBuilder.UseSqlServer("Server=.;Database=PaymentDb;Trusted_Connection=True;TrustServerCertificate=True;");

        return new PaymentDbContext(optionsBuilder.Options);
    }
}
