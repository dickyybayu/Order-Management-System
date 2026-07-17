using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CategoryEntity = global::OMS.API.Models.Category;
using ProductEntity = global::OMS.API.Models.Product;
using SupplierEntity = global::OMS.API.Models.Supplier;
namespace OMS.API.Infrastructure.Databases.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<ProductEntity>
{
    public void Configure(EntityTypeBuilder<ProductEntity> builder)
    {
        builder.ToTable("Products");

        builder.HasKey(product => product.Id);

        builder.Property(product => product.SKU)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(product => product.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(product => product.Unit)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(product => product.Price)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(product => product.Stock)
            .IsRequired();

        builder.Property(product => product.IsActive)
            .IsRequired();

        builder.Property(product => product.CreatedAtUtc)
            .IsRequired();

        builder.Property(product => product.UpdatedAtUtc);

        builder.Property(product => product.RowVersion)
            .IsRowVersion();

        builder.HasOne(product => product.Category)
            .WithMany()
            .HasForeignKey(product => product.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(product => product.Supplier)
            .WithMany()
            .HasForeignKey(product => product.SupplierId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(product => product.SKU)
            .IsUnique();

        builder.HasIndex(product => product.Name);
    }
}
