using Microsoft.EntityFrameworkCore;
using OMS.API.Infrastructure.Configurations;
using OMS.API.Infrastructure.Databases;
using OMS.API.Infrastructure.Seeders;

namespace OMS.API.Extensions;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString("DefaultConnection")
            ?? configuration["OMS_CONNECTION_STRING"];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "SQL Server connection string is not configured. Set ConnectionStrings:DefaultConnection or OMS_CONNECTION_STRING.");
        }

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseSqlServer(connectionString);
        });
        services.Configure<DevelopmentAdminOptions>(configuration.GetSection(DevelopmentAdminOptions.SectionName));
        services.AddScoped<IDatabaseInitializer, DatabaseInitializer>();

        return services;
    }
}
