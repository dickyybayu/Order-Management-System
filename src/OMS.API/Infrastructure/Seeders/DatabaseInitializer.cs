using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OMS.API.Constants.Permission;
using OMS.API.Domain.Auth.Services;
using OMS.API.Infrastructure.Databases;
using OMS.API.Infrastructure.Configurations;
using RoleEntity = global::OMS.API.Models.Role;
using UserEntity = global::OMS.API.Models.User;
namespace OMS.API.Infrastructure.Seeders;

public sealed class DatabaseInitializer(
    ApplicationDbContext dbContext,
    IOptions<DevelopmentAdminOptions> developmentAdminOptions,
    IPasswordHasher passwordHasher,
    ILogger<DatabaseInitializer> logger) : IDatabaseInitializer
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.Database.MigrateAsync(cancellationToken);
        await SeedRolesAsync(cancellationToken);
        await PrepareDevelopmentAdminAsync(cancellationToken);
    }

    private async Task SeedRolesAsync(CancellationToken cancellationToken)
    {
        var existingRoleNames = await dbContext.Roles
            .Select(role => role.Name)
            .ToListAsync(cancellationToken);
        var existingRoleNameSet = existingRoleNames.ToHashSet(StringComparer.Ordinal);
        var missingRoles = GetMissingRoleNames(existingRoleNameSet)
            .Select(roleName => new RoleEntity { Name = roleName })
            .ToArray();

        if (missingRoles.Length == 0)
        {
            logger.LogInformation("System roles are already seeded.");
            return;
        }

        dbContext.Roles.AddRange(missingRoles);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeded {RoleCount} system roles.", missingRoles.Length);
    }

    public static IReadOnlyCollection<string> GetMissingRoleNames(IReadOnlySet<string> existingRoleNames)
    {
        return SystemRoleNames.All
            .Where(roleName => !existingRoleNames.Contains(roleName))
            .ToArray();
    }

    private async Task PrepareDevelopmentAdminAsync(CancellationToken cancellationToken)
    {
        var options = developmentAdminOptions.Value;

        if (!options.Enabled)
        {
            return;
        }

        if (!options.HasRequiredConfiguration())
        {
            logger.LogWarning(
                "Development admin seeding is enabled but required configuration is missing. Admin user was not created.");
            return;
        }

        var normalizedEmail = UserEntity.NormalizeEmail(options.Email!);
        var adminRole = await dbContext.Roles
            .SingleOrDefaultAsync(role => role.Name == SystemRoleNames.Admin, cancellationToken);

        if (adminRole is null)
        {
            logger.LogWarning(
                "Development admin seeding is enabled but the admin role does not exist. Admin user was not created.");
            return;
        }

        var adminExists = await dbContext.Users
            .AnyAsync(user => user.Email == normalizedEmail, cancellationToken);

        if (adminExists)
        {
            logger.LogInformation("Development admin user already exists.");
            return;
        }

        dbContext.Users.Add(CreateDevelopmentAdminUser(
            options,
            adminRole.Id,
            normalizedEmail,
            passwordHasher));
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Development admin user {Email} was created.",
            normalizedEmail);
    }

    public static UserEntity CreateDevelopmentAdminUser(
        DevelopmentAdminOptions options,
        Guid adminRoleId,
        string normalizedEmail,
        IPasswordHasher passwordHasher)
    {
        return new UserEntity
        {
            Email = normalizedEmail,
            FullName = options.FullName!.Trim(),
            PasswordHash = passwordHasher.HashPassword(options.Password!),
            RoleId = adminRoleId,
            IsActive = true
        };
    }
}
