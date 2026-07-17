using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderStatusHistoryEntity = global::OMS.API.Models.OrderStatusHistory;
namespace OMS.API.Infrastructure.Databases.Configurations;

public sealed class OrderStatusHistoryConfiguration : IEntityTypeConfiguration<OrderStatusHistoryEntity>
{
    public void Configure(EntityTypeBuilder<OrderStatusHistoryEntity> builder)
    {
        builder.ToTable("OrderStatusHistories");

        builder.HasKey(history => history.Id);

        builder.Property(history => history.Id)
            .ValueGeneratedNever();

        builder.Property(history => history.FromStatus)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(history => history.ToStatus)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(history => history.Note)
            .HasMaxLength(500);

        builder.Property(history => history.ChangedAtUtc)
            .IsRequired();

        builder.HasOne(history => history.ChangedByUser)
            .WithMany()
            .HasForeignKey(history => history.ChangedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(history => history.OrderId);

        builder.HasIndex(history => history.ChangedByUserId);

        builder.HasIndex(history => history.ChangedAtUtc);
    }
}
