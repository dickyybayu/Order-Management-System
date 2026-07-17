using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using OMS.API.Infrastructure.Configurations;
using OMS.API.Infrastructure.Middlewares;

namespace OMS.API.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiFoundation(this IServiceCollection services)
    {
        services
            .AddControllers(options =>
            {
                options.Conventions.Add(new ApiRoutePrefixConvention("api/v1"));
            })
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var problemDetails = new ValidationProblemDetails(context.ModelState)
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Validation failed",
                    Detail = "One or more fields are invalid.",
                    Type = "https://httpstatuses.com/400"
                };

                problemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

                return new BadRequestObjectResult(problemDetails);
            };
        });

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Order Management System API",
                Version = "v1",
                Description = "REST API for authentication, master data, order lifecycle management, reporting jobs, and exchange-rate lookup."
            });
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Enter a JWT bearer token."
            });
            options.OperationFilter<SwaggerDefaultValuesOperationFilter>();

            var xmlDocumentationFile = $"{typeof(Program).Assembly.GetName().Name}.xml";
            var xmlDocumentationPath = Path.Combine(AppContext.BaseDirectory, xmlDocumentationFile);

            if (File.Exists(xmlDocumentationPath))
            {
                options.IncludeXmlComments(xmlDocumentationPath);
            }
        });
        services.AddHealthChecks();
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
            };
        });
        services.AddExceptionHandler<GlobalExceptionHandler>();

        return services;
    }
}
