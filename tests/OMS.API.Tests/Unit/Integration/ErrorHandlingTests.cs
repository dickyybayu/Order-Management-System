namespace OMS.API.Tests.Unit;

public sealed class ErrorHandlingTests : TestBase
{
    [Theory]
    [MemberData(nameof(ExceptionStatusCases))]
    public async Task GlobalExceptionHandlerMapsApplicationExceptions(Exception exception, int expectedStatusCode)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
            };
        });

        await using var serviceProvider = services.BuildServiceProvider();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = serviceProvider,
            TraceIdentifier = "trace-test",
            Response =
            {
                Body = new MemoryStream()
            }
        };
        var handler = new GlobalExceptionHandler(
            serviceProvider.GetRequiredService<IProblemDetailsService>(),
            NullLogger<GlobalExceptionHandler>.Instance);

        var handled = await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        httpContext.Response.Body.Position = 0;
        using var problemJson = await JsonDocument.ParseAsync(httpContext.Response.Body);

        Assert.True(handled);
        Assert.Equal(expectedStatusCode, httpContext.Response.StatusCode);
        Assert.Equal(expectedStatusCode, problemJson.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("trace-test", problemJson.RootElement.GetProperty("traceId").GetString());
    }


    [Fact]
    public void ApiFoundationReturnsValidationProblemDetailsWithFieldErrors()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApiFoundation();
        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<ApiBehaviorOptions>>().Value;
        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "validation-trace"
        };
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor(),
            new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary());
        actionContext.ModelState.AddModelError("email", "Email is required.");

        var result = Assert.IsType<BadRequestObjectResult>(options.InvalidModelStateResponseFactory(actionContext));
        var problemDetails = Assert.IsType<ValidationProblemDetails>(result.Value);

        Assert.Equal(StatusCodes.Status400BadRequest, problemDetails.Status);
        Assert.True(problemDetails.Errors.ContainsKey("email"));
        Assert.Equal("validation-trace", problemDetails.Extensions["traceId"]);
    }

}

