using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BackgroundJobExecutionEntity = global::OMS.API.Models.BackgroundJobExecution;
namespace OMS.API.Infrastructure.Databases.Configurations;

public sealed class BackgroundJobExecutionConfiguration : IEntityTypeConfiguration<BackgroundJobExecutionEntity>
{
    public void Configure(EntityTypeBuilder<BackgroundJobExecutionEntity> builder)
    {
        builder.ToTable("BackgroundJobExecutions");

        builder.HasKey(execution => execution.Id);

        builder.Property(execution => execution.JobName)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(execution => execution.StartedAtUtc)
            .IsRequired();

        builder.Property(execution => execution.FinishedAtUtc);

        builder.Property(execution => execution.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(execution => execution.Message)
            .HasColumnType("nvarchar(max)");

        builder.HasIndex(execution => execution.JobName);

        builder.HasIndex(execution => execution.StartedAtUtc);

        builder.HasIndex(execution => execution.Status);
    }
}
