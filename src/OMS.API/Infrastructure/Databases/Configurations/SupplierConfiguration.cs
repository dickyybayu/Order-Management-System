using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupplierEntity = global::OMS.API.Models.Supplier;
namespace OMS.API.Infrastructure.Databases.Configurations;

public sealed class SupplierConfiguration : IEntityTypeConfiguration<SupplierEntity>
{
    public void Configure(EntityTypeBuilder<SupplierEntity> builder)
    {
        builder.ToTable("Suppliers");

        builder.HasKey(supplier => supplier.Id);

        builder.Property(supplier => supplier.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(supplier => supplier.Email)
            .HasMaxLength(255);

        builder.Property(supplier => supplier.Phone)
            .HasMaxLength(30);

        builder.Property(supplier => supplier.Address)
            .HasMaxLength(500);

        builder.Property(supplier => supplier.IsActive)
            .IsRequired();

        builder.Property(supplier => supplier.CreatedAtUtc)
            .IsRequired();

        builder.Property(supplier => supplier.UpdatedAtUtc);
    }
}
