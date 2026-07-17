namespace OMS.API.Tests.Unit;

public sealed class BootstrapTests : TestBase
{
    [Fact]
    public void ApiAssemblyCanBeLoaded()
    {
        var assembly = Assembly.Load("OMS.API");

        Assert.Equal("OMS.API", assembly.GetName().Name);
    }


    [Fact]
    public void PersistenceServicesRequireConnectionString()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddPersistence(configuration));

        Assert.Contains("connection string", exception.Message, StringComparison.OrdinalIgnoreCase);
    }


    [Fact]
    public async Task AuthenticatedUserCanListAndGetCategories()
    {
        await using var factory = new CategoryApplicationFactory();
        using var client = factory.CreateClient();
        var user = CreateTokenUser(SystemRoleNames.SalesOperator);
        var token = new JwtTokenService(Options.Create(TestApplicationFactory.JwtOptions))
            .CreateAccessToken(user, SystemRoleNames.SalesOperator);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            token.AccessToken);

        var listResponse = await client.GetAsync("/api/v1/categories");
        var getResponse = await client.GetAsync($"/api/v1/categories/{CategoryApplicationFactory.CategoryId}");
        var listBody = await listResponse.Content.ReadAsStringAsync();
        var getBody = await getResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Contains("Hardware", listBody, StringComparison.Ordinal);
        Assert.Contains("Hardware", getBody, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(Category.NormalizeNameForStorage), listBody, StringComparison.Ordinal);
    }


    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    public async Task NonAdminCannotWriteCategories(string method)
    {
        await using var factory = new CategoryApplicationFactory();
        using var client = factory.CreateClient();
        var user = CreateTokenUser(SystemRoleNames.SalesOperator);
        var token = new JwtTokenService(Options.Create(TestApplicationFactory.JwtOptions))
            .CreateAccessToken(user, SystemRoleNames.SalesOperator);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            token.AccessToken);
        using var request = method switch
        {
            "POST" => new HttpRequestMessage(HttpMethod.Post, "/api/v1/categories")
            {
                Content = CreateJsonContent(new CreateCategoryRequest("Hardware", null))
            },
            "PUT" => new HttpRequestMessage(
                HttpMethod.Put,
                $"/api/v1/categories/{CategoryApplicationFactory.CategoryId}")
            {
                Content = CreateJsonContent(new UpdateCategoryRequest("Hardware", null))
            },
            _ => new HttpRequestMessage(
                HttpMethod.Patch,
                $"/api/v1/categories/{CategoryApplicationFactory.CategoryId}/status")
            {
                Content = CreateJsonContent(new UpdateCategoryStatusRequest(false))
            }
        };

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }


    [Fact]
    public void PaginatedResultMatchesDocumentedResponseShape()
    {
        var metadata = new PaginationMetadata(page: 1, pageSize: 2, totalItems: 3);
        var result = new PaginatedResult<string>(["first", "second"], metadata);

        Assert.Equal(["first", "second"], result.Items);
        Assert.Same(metadata, result.Pagination);
    }

}

