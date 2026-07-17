using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DailySalesReportEntity = global::OMS.API.Models.DailySalesReport;
namespace OMS.API.Infrastructure.Databases.Configurations;

public sealed class DailySalesReportConfiguration : IEntityTypeConfiguration<DailySalesReportEntity>
{
    public void Configure(EntityTypeBuilder<DailySalesReportEntity> builder)
    {
        builder.ToTable("DailySalesReports");

        builder.HasKey(report => report.Id);

        builder.Property(report => report.ReportDate)
            .IsRequired();

        builder.Property(report => report.TotalOrders)
            .IsRequired();

        builder.Property(report => report.TotalRevenue)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(report => report.GeneratedAtUtc)
            .IsRequired();

        builder.HasMany(report => report.Items)
            .WithOne(item => item.DailySalesReport)
            .HasForeignKey(item => item.DailySalesReportId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(report => report.ReportDate)
            .IsUnique();
    }
}
