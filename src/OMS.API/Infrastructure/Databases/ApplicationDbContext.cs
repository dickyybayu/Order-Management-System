using Microsoft.EntityFrameworkCore;
using AuditableEntityEntity = global::OMS.API.Models.AuditableEntity;
using BackgroundJobExecutionEntity = global::OMS.API.Models.BackgroundJobExecution;
using CategoryEntity = global::OMS.API.Models.Category;
using CustomerEntity = global::OMS.API.Models.Customer;
using DailySalesReportEntity = global::OMS.API.Models.DailySalesReport;
using DailySalesReportItemEntity = global::OMS.API.Models.DailySalesReportItem;
using OrderEntity = global::OMS.API.Models.Order;
using OrderItemEntity = global::OMS.API.Models.OrderItem;
using OrderStatusHistoryEntity = global::OMS.API.Models.OrderStatusHistory;
using ProductEntity = global::OMS.API.Models.Product;
using RoleEntity = global::OMS.API.Models.Role;
using SupplierEntity = global::OMS.API.Models.Supplier;
using UserEntity = global::OMS.API.Models.User;
namespace OMS.API.Infrastructure.Databases;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<RoleEntity> Roles => Set<RoleEntity>();

    public DbSet<UserEntity> Users => Set<UserEntity>();

    public DbSet<CategoryEntity> Categories => Set<CategoryEntity>();

    public DbSet<SupplierEntity> Suppliers => Set<SupplierEntity>();

    public DbSet<ProductEntity> Products => Set<ProductEntity>();

    public DbSet<CustomerEntity> Customers => Set<CustomerEntity>();

    public DbSet<OrderEntity> Orders => Set<OrderEntity>();

    public DbSet<OrderItemEntity> OrderItems => Set<OrderItemEntity>();

    public DbSet<OrderStatusHistoryEntity> OrderStatusHistories => Set<OrderStatusHistoryEntity>();

    public DbSet<DailySalesReportEntity> DailySalesReports => Set<DailySalesReportEntity>();

    public DbSet<DailySalesReportItemEntity> DailySalesReportItems => Set<DailySalesReportItemEntity>();

    public DbSet<BackgroundJobExecutionEntity> BackgroundJobExecutions => Set<BackgroundJobExecutionEntity>();

    public override int SaveChanges()
    {
        PrepareEntitiesForSave();

        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        PrepareEntitiesForSave();

        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }

    private void PrepareEntitiesForSave()
    {
        SetAuditTimestamps();
        NormalizeUserEmails();
        NormalizeCategoryNames();
        TrimSupplierStringFields();
        NormalizeProductFields();
        NormalizeCustomerFields();
        PrepareOrderFields();
        PrepareOrderItemFields();
        PrepareOrderStatusHistoryFields();
        PrepareDailySalesReportItems();
        PrepareBackgroundJobExecutions();
    }

    private void SetAuditTimestamps()
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<AuditableEntityEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAtUtc = now;
                entry.Entity.UpdatedAtUtc = null;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(entity => entity.CreatedAtUtc).IsModified = false;
                entry.Entity.UpdatedAtUtc = now;
            }
        }
    }

    private void NormalizeUserEmails()
    {
        foreach (var entry in ChangeTracker.Entries<UserEntity>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Entity.NormalizeEmailForStorage();
            }
        }
    }

    private void NormalizeCategoryNames()
    {
        foreach (var entry in ChangeTracker.Entries<CategoryEntity>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Entity.NormalizeNameForStorage();
            }
        }
    }

    private void TrimSupplierStringFields()
    {
        foreach (var entry in ChangeTracker.Entries<SupplierEntity>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Entity.TrimStringFieldsForStorage();
            }
        }
    }

    private void NormalizeProductFields()
    {
        foreach (var entry in ChangeTracker.Entries<ProductEntity>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Entity.NormalizeForStorage();
            }
        }
    }

    private void NormalizeCustomerFields()
    {
        foreach (var entry in ChangeTracker.Entries<CustomerEntity>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Entity.NormalizeForStorage();
            }
        }
    }

    private void PrepareOrderFields()
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<OrderEntity>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Entity.NormalizeForStorage();
            }

            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAtUtc = now;
                entry.Entity.UpdatedAtUtc = null;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(order => order.CreatedAtUtc).IsModified = false;
                entry.Entity.UpdatedAtUtc = now;
            }
        }
    }

    private void PrepareOrderItemFields()
    {
        foreach (var entry in ChangeTracker.Entries<OrderItemEntity>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Entity.NormalizeForStorage();
            }
        }
    }

    private void PrepareOrderStatusHistoryFields()
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<OrderStatusHistoryEntity>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Entity.NormalizeForStorage();
            }

            if (entry.State == EntityState.Added && entry.Entity.ChangedAtUtc == default)
            {
                entry.Entity.ChangedAtUtc = now;
            }
        }
    }

    private void PrepareDailySalesReportItems()
    {
        foreach (var entry in ChangeTracker.Entries<DailySalesReportItemEntity>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Entity.NormalizeForStorage();
            }
        }
    }

    private void PrepareBackgroundJobExecutions()
    {
        foreach (var entry in ChangeTracker.Entries<BackgroundJobExecutionEntity>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Entity.NormalizeForStorage();
            }
        }
    }
}
