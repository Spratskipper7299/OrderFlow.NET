using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OrderFlow.Order.Infrastructure.Data.Configurations;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Domain.Orders.Order>
{
    public void Configure(EntityTypeBuilder<Domain.Orders.Order> builder)
    {
        builder.ToTable("Orders");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Status)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(o => o.RejectionReason)
            .HasMaxLength(500);

        builder.Property(o => o.RowVersion)
            .IsRowVersion();

        builder.OwnsMany(o => o.Lines, lineBuilder =>
        {
            lineBuilder.ToTable("OrderLines");
            lineBuilder.WithOwner().HasForeignKey("OrderId");
            lineBuilder.HasKey("Id");
            lineBuilder.Property<Guid>("Id").ValueGeneratedOnAdd();

            lineBuilder.Property(l => l.ProductId);
            lineBuilder.Property(l => l.Quantity);
            lineBuilder.Property(l => l.UnitPrice)
                .HasColumnType("decimal(18,2)");
        });

        builder.Navigation(o => o.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
