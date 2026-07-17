using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace OMS.API.Infrastructure.Configurations;

public sealed class SwaggerDefaultValuesOperationFilter : IOperationFilter
{
    private static readonly OpenApiReference BearerReference = new()
    {
        Type = ReferenceType.SecurityScheme,
        Id = "Bearer"
    };

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        operation.Summary ??= BuildSummary(context);
        operation.Description ??= BuildDescription(context);

        AddProblemDetailsResponses(operation, context);
        AddSecurityRequirement(operation, context);
    }

    private static void AddSecurityRequirement(OpenApiOperation operation, OperationFilterContext context)
    {
        var hasAuthorize = context.MethodInfo.DeclaringType?.GetCustomAttributes(true).OfType<AuthorizeAttribute>().Any() == true ||
            context.MethodInfo.GetCustomAttributes(true).OfType<AuthorizeAttribute>().Any();
        var hasAllowAnonymous = context.MethodInfo.DeclaringType?.GetCustomAttributes(true).OfType<AllowAnonymousAttribute>().Any() == true ||
            context.MethodInfo.GetCustomAttributes(true).OfType<AllowAnonymousAttribute>().Any();

        if (!hasAuthorize || hasAllowAnonymous)
        {
            return;
        }

        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecurityScheme { Reference = BearerReference }] = []
        });

        operation.Responses.TryAdd("401", CreateProblemDetailsResponse("Unauthorized"));
    }

    private static void AddProblemDetailsResponses(OpenApiOperation operation, OperationFilterContext context)
    {
        var producesResponses = context.ApiDescription.SupportedResponseTypes
            .Select(response => response.StatusCode)
            .ToHashSet();

        operation.Responses.TryAdd("400", CreateProblemDetailsResponse("Validation failed"));

        foreach (var statusCode in producesResponses)
        {
            if (statusCode is >= 400)
            {
                operation.Responses.TryAdd(statusCode.ToString(), CreateProblemDetailsResponse(GetTitle(statusCode)));
            }
        }

        if (HasAuthorizationPolicy(context))
        {
            operation.Responses.TryAdd("403", CreateProblemDetailsResponse("Forbidden"));
        }

        if (context.MethodInfo.DeclaringType?.Name.Equals("ExchangeRatesController", StringComparison.Ordinal) == true)
        {
            operation.Responses.TryAdd("503", CreateProblemDetailsResponse("External service unavailable"));
        }

        operation.Responses.TryAdd("500", CreateProblemDetailsResponse("Internal server error"));
    }

    private static bool HasAuthorizationPolicy(OperationFilterContext context)
    {
        return context.MethodInfo.DeclaringType?.GetCustomAttributes(true)
                .OfType<AuthorizeAttribute>()
                .Any(attribute => !string.IsNullOrWhiteSpace(attribute.Policy)) == true ||
            context.MethodInfo.GetCustomAttributes(true)
                .OfType<AuthorizeAttribute>()
                .Any(attribute => !string.IsNullOrWhiteSpace(attribute.Policy));
    }

    private static OpenApiResponse CreateProblemDetailsResponse(string description)
    {
        return new OpenApiResponse
        {
            Description = description,
            Content =
            {
                ["application/problem+json"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.Schema,
                            Id = nameof(ProblemDetails)
                        }
                    }
                }
            }
        };
    }

    private static string GetTitle(int statusCode)
    {
        return statusCode switch
        {
            StatusCodes.Status400BadRequest => "Validation failed",
            StatusCodes.Status401Unauthorized => "Unauthorized",
            StatusCodes.Status403Forbidden => "Forbidden",
            StatusCodes.Status404NotFound => "Resource not found",
            StatusCodes.Status409Conflict => "Conflict",
            StatusCodes.Status422UnprocessableEntity => "Business rule violation",
            StatusCodes.Status503ServiceUnavailable => "External service unavailable",
            _ => "Error"
        };
    }

    private static string BuildSummary(OperationFilterContext context)
    {
        var controller = context.MethodInfo.DeclaringType?.Name.Replace("Controller", string.Empty, StringComparison.Ordinal) ?? "API";
        var action = context.MethodInfo.Name;

        return $"{controller} {action}";
    }

    private static string BuildDescription(OperationFilterContext context)
    {
        var controller = context.MethodInfo.DeclaringType?.Name.Replace("Controller", string.Empty, StringComparison.Ordinal) ?? "API";

        return controller switch
        {
            "Auth" => "Authentication endpoints for registration and JWT login.",
            "Users" => "Administrative user-management endpoints.",
            "Categories" => "Category master-data endpoints.",
            "Suppliers" => "Supplier master-data endpoints.",
            "Products" => "Product catalog and stock master-data endpoints.",
            "Customers" => "Customer master-data endpoints.",
            "Orders" => "Order inquiry and lifecycle endpoints.",
            "ExchangeRates" => "Currency exchange-rate lookup endpoints backed by Frankfurter.",
            _ => "OMS API endpoint."
        };
    }
}
