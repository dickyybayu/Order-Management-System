using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CustomerEntity = global::OMS.API.Models.Customer;
namespace OMS.API.Infrastructure.Databases.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<CustomerEntity>
{
    public void Configure(EntityTypeBuilder<CustomerEntity> builder)
    {
        builder.ToTable("Customers");

        builder.HasKey(customer => customer.Id);

        builder.Property(customer => customer.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(customer => customer.Email)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(customer => customer.Phone)
            .HasMaxLength(30);

        builder.Property(customer => customer.ShippingAddress)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(customer => customer.IsActive)
            .IsRequired();

        builder.Property(customer => customer.CreatedAtUtc)
            .IsRequired();

        builder.Property(customer => customer.UpdatedAtUtc);

        builder.HasIndex(customer => customer.Email)
            .IsUnique();

        builder.HasIndex(customer => customer.Name);
    }
}
