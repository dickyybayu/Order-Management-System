namespace OMS.API.Tests.Unit;

public sealed class RequestLoggingMiddlewareTests : TestBase
{
    [Fact]
    public async Task RequestLoggingMiddlewareLogsStructuredRequestFields()
    {
        var logger = new TestLogger<RequestLoggingMiddleware>();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Get;
        httpContext.Request.Path = "/health";
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "user-123")],
            authenticationType: "Test"));
        var middleware = new RequestLoggingMiddleware(
            next: context =>
            {
                context.Response.StatusCode = StatusCodes.Status204NoContent;
                return Task.CompletedTask;
            },
            logger);

        await middleware.InvokeAsync(httpContext);

        var logEntry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, logEntry.LogLevel);
        Assert.Equal("GET", logEntry.State["HttpMethod"]);
        Assert.Equal("/health", logEntry.State["RequestPath"]);
        Assert.Equal(StatusCodes.Status204NoContent, logEntry.State["StatusCode"]);
        Assert.Equal("user-123", logEntry.State["UserId"]);
        Assert.True(Convert.ToDouble(logEntry.State["DurationMilliseconds"]) >= 0);
    }


    [Fact]
    public async Task RequestLoggingMiddlewareDoesNotLogSensitiveHeadersOrValues()
    {
        var logger = new TestLogger<RequestLoggingMiddleware>();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Post;
        httpContext.Request.Path = "/api/v1/auth/login";
        httpContext.Request.Headers.Authorization = "Bearer secret-token";
        httpContext.Request.Headers.Cookie = "session=secret-cookie";
        var middleware = new RequestLoggingMiddleware(
            next: context =>
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return Task.CompletedTask;
            },
            logger);

        await middleware.InvokeAsync(httpContext);

        var renderedLog = logger.Entries.Single().Message;

        Assert.DoesNotContain("Authorization", renderedLog, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret-token", renderedLog, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Cookie", renderedLog, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret-cookie", renderedLog, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", renderedLog, StringComparison.OrdinalIgnoreCase);
    }


    [Fact]
    public async Task RequestLoggingMiddlewareLogsWhenRequestFails()
    {
        var logger = new TestLogger<RequestLoggingMiddleware>();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Get;
        httpContext.Request.Path = "/api/v1/failure";
        var middleware = new RequestLoggingMiddleware(
            next: _ => throw new InvalidOperationException("Simulated failure."),
            logger);

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(httpContext));

        var logEntry = Assert.Single(logger.Entries);
        Assert.Equal("GET", logEntry.State["HttpMethod"]);
        Assert.Equal("/api/v1/failure", logEntry.State["RequestPath"]);
        Assert.True(logEntry.State.ContainsKey("DurationMilliseconds"));
    }

}

