namespace OMS.API.Tests.Unit;

public sealed class SwaggerAuthTests : TestBase
{
    [Fact]
    public async Task SwaggerSecurityDefinitionExists()
    {
        await using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        var body = await response.Content.ReadAsStringAsync();
        using var swagger = JsonDocument.Parse(body);
        var securitySchemes = swagger.RootElement
            .GetProperty("components")
            .GetProperty("securitySchemes");
        var bearerScheme = securitySchemes.GetProperty("Bearer");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("http", bearerScheme.GetProperty("type").GetString());
        Assert.Equal("bearer", bearerScheme.GetProperty("scheme").GetString());
        Assert.Equal("JWT", bearerScheme.GetProperty("bearerFormat").GetString());
    }


    [Fact]
    public async Task SwaggerDocumentsMetadataSecurityProblemDetailsAndHiddenEndpoints()
    {
        await using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        var body = await response.Content.ReadAsStringAsync();
        using var swagger = JsonDocument.Parse(body);
        var root = swagger.RootElement;
        var paths = root.GetProperty("paths");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Order Management System API", root.GetProperty("info").GetProperty("title").GetString());
        Assert.Equal("v1", root.GetProperty("info").GetProperty("version").GetString());
        Assert.Contains("order lifecycle", root.GetProperty("info").GetProperty("description").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.True(root.GetProperty("components").GetProperty("schemas").TryGetProperty(nameof(ProblemDetails), out _));
        Assert.True(paths.GetProperty("/api/v1/orders").GetProperty("post").TryGetProperty("security", out _));
        Assert.True(paths.GetProperty("/api/v1/users").GetProperty("get").TryGetProperty("security", out _));
        Assert.False(paths.GetProperty("/api/v1/auth/register").GetProperty("post").TryGetProperty("security", out _));
        Assert.False(paths.GetProperty("/api/v1/auth/login").GetProperty("post").TryGetProperty("security", out _));
        Assert.True(paths.GetProperty("/api/v1/exchange-rates").GetProperty("get").GetProperty("responses").TryGetProperty("503", out _));
        Assert.True(paths.GetProperty("/api/v1/orders").GetProperty("post").GetProperty("responses").TryGetProperty("422", out _));
        Assert.DoesNotContain("PasswordHash", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SigningKey", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("auth-diagnostics", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("run-daily-sales-report", body, StringComparison.OrdinalIgnoreCase);
    }

}

