using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderItemEntity = global::OMS.API.Models.OrderItem;
using ProductEntity = global::OMS.API.Models.Product;
namespace OMS.API.Infrastructure.Databases.Configurations;

public sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItemEntity>
{
    public void Configure(EntityTypeBuilder<OrderItemEntity> builder)
    {
        builder.ToTable("OrderItems");

        builder.HasKey(item => item.Id);

        builder.Property(item => item.ProductSku)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(item => item.ProductName)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(item => item.Quantity)
            .IsRequired();

        builder.Property(item => item.UnitPrice)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(item => item.LineTotal)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.HasOne(item => item.Product)
            .WithMany()
            .HasForeignKey(item => item.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(item => item.OrderId);

        builder.HasIndex(item => item.ProductId);
    }
}
