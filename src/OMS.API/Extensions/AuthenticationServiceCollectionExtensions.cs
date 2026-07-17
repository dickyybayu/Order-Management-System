using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Polly;
using Polly.Extensions.Http;
using OMS.API.Constants.Permission;
using OMS.API.Infrastructure.Integrations.Http.Frankfurter;
using OMS.API.Infrastructure.Repositories.Auth;
using OMS.API.Infrastructure.Repositories.Category;
using OMS.API.Infrastructure.Repositories.Customer;
using OMS.API.Infrastructure.Repositories.Order;
using OMS.API.Infrastructure.Repositories.Product;
using OMS.API.Infrastructure.Repositories.Reporting;
using OMS.API.Infrastructure.Repositories.Supplier;
using OMS.API.Infrastructure.Repositories.User;
using OMS.API.Infrastructure.Queues;
using OMS.API.Domain.Auth.Repositories;
using OMS.API.Domain.Category.Repositories;
using OMS.API.Domain.Customer.Repositories;
using OMS.API.Domain.Order.Repositories;
using OMS.API.Domain.Product.Repositories;
using OMS.API.Domain.Reporting.Repositories;
using OMS.API.Domain.Supplier.Repositories;
using OMS.API.Domain.User.Repositories;
using OMS.API.Domain.Auth.Services;
using OMS.API.Domain.Auth.Token;
using OMS.API.Domain.Category.Services;
using OMS.API.Domain.Customer.Services;
using OMS.API.Domain.ExchangeRate.Services;
using OMS.API.Domain.Order.Services;
using OMS.API.Domain.Product.Services;
using OMS.API.Domain.Reporting.Services;
using OMS.API.Domain.Supplier.Services;
using OMS.API.Domain.User.Services;

namespace OMS.API.Extensions;

public static class AuthenticationServiceCollectionExtensions
{
    public static IServiceCollection AddOmsAuthenticationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddHttpContextAccessor();
        services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<ICurrentUserContext, HttpContextCurrentUserContext>();
        services.AddScoped<IAuthRepository, AuthRepository>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<ISupplierRepository, SupplierRepository>();
        services.AddScoped<ISupplierService, SupplierService>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IOrderNumberGenerator, OrderNumberGenerator>();
        services.AddScoped<IOrderStatusNotificationQueue, CoravelOrderStatusNotificationQueue>();
        services.AddScoped<IReportingRepository, ReportingRepository>();
        services.AddScoped<IReportingService, ReportingService>();
        services.AddScoped<IDailySalesReportGenerator, DailySalesReportGenerator>();
        services.Configure<OrderCurrencyOptions>(configuration.GetSection(OrderCurrencyOptions.SectionName));
        services.AddSingleton(serviceProvider =>
        {
            var configuredOptions = configuration.GetSection(OrderCurrencyOptions.SectionName).Get<OrderCurrencyOptions>()
                ?? new OrderCurrencyOptions();

            return configuredOptions;
        });
        services.Configure<FrankfurterOptions>(configuration.GetSection(FrankfurterOptions.SectionName));
        services.AddSingleton(serviceProvider =>
        {
            var configuredOptions = configuration.GetSection(FrankfurterOptions.SectionName).Get<FrankfurterOptions>()
                ?? new FrankfurterOptions();

            return configuredOptions;
        });
        services.AddHttpClient<IExchangeRateClient, FrankfurterExchangeRateClient>((serviceProvider, httpClient) =>
        {
            var frankfurterOptions = serviceProvider.GetRequiredService<FrankfurterOptions>();

            httpClient.BaseAddress = new Uri(frankfurterOptions.BaseUrl, UriKind.Absolute);
            httpClient.Timeout = TimeSpan.FromSeconds(frankfurterOptions.TimeoutSeconds);
        })
        .AddPolicyHandler((serviceProvider, _) => CreateFrankfurterRetryPolicy(serviceProvider))
        .AddPolicyHandler((serviceProvider, _) => CreateFrankfurterCircuitBreakerPolicy(serviceProvider));
        services.AddScoped<ICurrencyService, CurrencyService>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
                    ?? new JwtOptions();

                options.MapInboundClaims = false;
                options.TokenValidationParameters = CreateTokenValidationParameters(jwtOptions);
            });
        services.AddAuthorization(options =>
        {
            options.AddPolicy(
                AuthorizationPolicies.AdminOnly,
                policy => policy.RequireRole(SystemRoleNames.Admin));
            options.AddPolicy(
                AuthorizationPolicies.SupervisorOnly,
                policy => policy.RequireRole(SystemRoleNames.Supervisor));
            options.AddPolicy(
                AuthorizationPolicies.SalesOperatorOnly,
                policy => policy.RequireRole(SystemRoleNames.SalesOperator));
            options.AddPolicy(
                AuthorizationPolicies.CustomerWrite,
                policy => policy.RequireRole(SystemRoleNames.Admin, SystemRoleNames.SalesOperator));
            options.AddPolicy(
                AuthorizationPolicies.OrderCreate,
                policy => policy.RequireRole(SystemRoleNames.Admin, SystemRoleNames.SalesOperator));
            options.AddPolicy(
                AuthorizationPolicies.OrderApprove,
                policy => policy.RequireRole(SystemRoleNames.Admin, SystemRoleNames.Supervisor));
            options.AddPolicy(
                AuthorizationPolicies.OrderShip,
                policy => policy.RequireRole(SystemRoleNames.Admin, SystemRoleNames.Supervisor));
            options.AddPolicy(
                AuthorizationPolicies.OrderDeliver,
                policy => policy.RequireRole(SystemRoleNames.Admin, SystemRoleNames.Supervisor));
            options.AddPolicy(
                AuthorizationPolicies.OrderCancel,
                policy => policy.RequireRole(
                    SystemRoleNames.Admin,
                    SystemRoleNames.Supervisor,
                    SystemRoleNames.SalesOperator));
            options.AddPolicy(
                AuthorizationPolicies.ReportingRead,
                policy => policy.RequireRole(SystemRoleNames.Admin, SystemRoleNames.Supervisor));
        });

        return services;
    }

    public static TokenValidationParameters CreateTokenValidationParameters(JwtOptions jwtOptions)
    {
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = CreateSigningKey(jwtOptions.SigningKey),
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ClockSkew = TimeSpan.Zero,
            NameClaimType = System.Security.Claims.ClaimTypes.Name,
            RoleClaimType = System.Security.Claims.ClaimTypes.Role
        };
    }

    private static SymmetricSecurityKey? CreateSigningKey(string? signingKey)
    {
        if (string.IsNullOrWhiteSpace(signingKey))
        {
            return null;
        }

        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
    }

    private static IAsyncPolicy<HttpResponseMessage> CreateFrankfurterRetryPolicy(IServiceProvider serviceProvider)
    {
        var options = serviceProvider.GetRequiredService<FrankfurterOptions>();
        var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("FrankfurterHttpPolicy");

        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(response => response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(
                options.RetryCount,
                attempt => TimeSpan.FromMilliseconds(100 * Math.Pow(2, attempt - 1)),
                (outcome, delay, attempt, _) =>
                {
                    logger.LogWarning(
                        outcome.Exception,
                        "Retrying Frankfurter request attempt {Attempt} after {DelayMilliseconds}ms due to {FailureReason}",
                        attempt,
                        delay.TotalMilliseconds,
                        outcome.Exception?.GetType().Name ?? ((int)outcome.Result.StatusCode).ToString());
                });
    }

    private static IAsyncPolicy<HttpResponseMessage> CreateFrankfurterCircuitBreakerPolicy(IServiceProvider serviceProvider)
    {
        var options = serviceProvider.GetRequiredService<FrankfurterOptions>();
        var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("FrankfurterHttpPolicy");

        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(response => response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            .CircuitBreakerAsync(
                options.CircuitBreakerFailureThreshold,
                TimeSpan.FromSeconds(options.CircuitBreakerDurationSeconds),
                (outcome, duration) =>
                {
                    logger.LogWarning(
                        outcome.Exception,
                        "Frankfurter circuit opened for {BreakDurationSeconds}s due to {FailureReason}",
                        duration.TotalSeconds,
                        outcome.Exception?.GetType().Name ?? ((int)outcome.Result.StatusCode).ToString());
                },
                () => logger.LogInformation("Frankfurter circuit reset."),
                () => logger.LogInformation("Frankfurter circuit is half-open."));
    }
}
