using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CustomerEntity = global::OMS.API.Models.Customer;
using OrderEntity = global::OMS.API.Models.Order;
namespace OMS.API.Infrastructure.Databases.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<OrderEntity>
{
    public void Configure(EntityTypeBuilder<OrderEntity> builder)
    {
        builder.ToTable("Orders");

        builder.HasKey(order => order.Id);

        builder.Property(order => order.OrderNumber)
            .IsRequired()
            .HasMaxLength(40);

        builder.Property(order => order.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(order => order.TrackingNumber)
            .HasMaxLength(100);

        builder.Property(order => order.CurrencyCode)
            .IsRequired()
            .HasColumnType("char(3)");

        builder.Property(order => order.ExchangeRate)
            .HasPrecision(18, 6);

        builder.Property(order => order.Subtotal)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(order => order.TotalAmount)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(order => order.CreatedAtUtc)
            .IsRequired();

        builder.Property(order => order.UpdatedAtUtc);

        builder.Property(order => order.CancelledAtUtc);

        builder.HasOne(order => order.Customer)
            .WithMany()
            .HasForeignKey(order => order.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(order => order.CreatedByUser)
            .WithMany()
            .HasForeignKey(order => order.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(order => order.Items)
            .WithOne(item => item.Order)
            .HasForeignKey(item => item.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(order => order.StatusHistory)
            .WithOne(history => history.Order)
            .HasForeignKey(history => history.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(order => order.OrderNumber)
            .IsUnique();

        builder.HasIndex(order => order.Status);

        builder.HasIndex(order => order.CustomerId);

        builder.HasIndex(order => order.CreatedByUserId);

        builder.HasIndex(order => order.CreatedAtUtc);

        builder.HasIndex(order => new { order.Status, order.CustomerId, order.CreatedAtUtc });
    }
}
