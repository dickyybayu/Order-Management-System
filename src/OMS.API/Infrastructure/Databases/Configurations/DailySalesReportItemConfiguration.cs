using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DailySalesReportItemEntity = global::OMS.API.Models.DailySalesReportItem;
using ProductEntity = global::OMS.API.Models.Product;
namespace OMS.API.Infrastructure.Databases.Configurations;

public sealed class DailySalesReportItemConfiguration : IEntityTypeConfiguration<DailySalesReportItemEntity>
{
    public void Configure(EntityTypeBuilder<DailySalesReportItemEntity> builder)
    {
        builder.ToTable("DailySalesReportItems");

        builder.HasKey(item => item.Id);

        builder.Property(item => item.ProductSku)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(item => item.ProductName)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(item => item.QuantitySold)
            .IsRequired();

        builder.Property(item => item.Revenue)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.HasOne(item => item.Product)
            .WithMany()
            .HasForeignKey(item => item.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(item => item.DailySalesReportId);

        builder.HasIndex(item => item.ProductId);
    }
}
