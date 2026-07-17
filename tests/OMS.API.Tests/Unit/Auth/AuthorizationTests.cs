namespace OMS.API.Tests.Unit;

public sealed class AuthorizationTests : TestBase
{
    [Fact]
    public async Task ProtectedEndpointReturnsUnauthorizedWithoutToken()
    {
        await using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/auth-diagnostics/protected");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }


    [Fact]
    public async Task ProtectedEndpointAllowsValidToken()
    {
        await using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();
        var user = CreateTokenUser(SystemRoleNames.SalesOperator);
        var token = new JwtTokenService(Options.Create(TestApplicationFactory.JwtOptions))
            .CreateAccessToken(user, SystemRoleNames.SalesOperator);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            token.AccessToken);

        var response = await client.GetAsync("/api/v1/auth-diagnostics/protected");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{body}{Environment.NewLine}{GetIntegrationLogs()}");
        Assert.DoesNotContain(token.AccessToken, body, StringComparison.Ordinal);
        Assert.Contains(user.Email, body, StringComparison.Ordinal);
        Assert.Contains(SystemRoleNames.SalesOperator, body, StringComparison.Ordinal);
    }


    [Fact]
    public async Task RoleProtectedEndpointReturnsForbiddenForWrongRole()
    {
        await using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();
        var user = CreateTokenUser(SystemRoleNames.SalesOperator);
        var token = new JwtTokenService(Options.Create(TestApplicationFactory.JwtOptions))
            .CreateAccessToken(user, SystemRoleNames.SalesOperator);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            token.AccessToken);

        var response = await client.GetAsync("/api/v1/auth-diagnostics/admin");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }


    [Fact]
    public async Task RoleProtectedEndpointAllowsCorrectRole()
    {
        await using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();
        var user = CreateTokenUser(SystemRoleNames.Admin);
        var token = new JwtTokenService(Options.Create(TestApplicationFactory.JwtOptions))
            .CreateAccessToken(user, SystemRoleNames.Admin);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            token.AccessToken);

        var response = await client.GetAsync("/api/v1/auth-diagnostics/admin");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }


    [Fact]
    public void CurrentUserContextReadsExpectedClaims()
    {
        var userId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                    new Claim(ClaimTypes.Email, "sales@example.com"),
                    new Claim(ClaimTypes.Name, "Sales User"),
                    new Claim(ClaimTypes.Role, SystemRoleNames.SalesOperator)
                ],
                authenticationType: "Test"))
        };
        var currentUser = new HttpContextCurrentUserContext(new HttpContextAccessor
        {
            HttpContext = httpContext
        });

        Assert.True(currentUser.IsAuthenticated);
        Assert.Equal(userId, currentUser.UserId);
        Assert.Equal(userId, currentUser.GetRequiredUserId());
        Assert.Equal("sales@example.com", currentUser.Email);
        Assert.Equal("Sales User", currentUser.FullName);
        Assert.Equal(SystemRoleNames.SalesOperator, currentUser.Role);
    }


    [Fact]
    public void CurrentUserContextDoesNotThrowForAnonymousUsers()
    {
        var currentUser = new HttpContextCurrentUserContext(new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext()
        });

        Assert.False(currentUser.IsAuthenticated);
        Assert.Null(currentUser.UserId);
        Assert.Null(currentUser.Email);
        Assert.Null(currentUser.FullName);
        Assert.Null(currentUser.Role);
        Assert.Throws<InvalidOperationException>(() => currentUser.GetRequiredUserId());
    }


    [Theory]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("signature")]
    [InlineData("expired")]
    public async Task InvalidJwtIsRejected(string invalidPart)
    {
        await using var factory = new TestApplicationFactory();
        using var client = factory.CreateClient();
        var token = invalidPart switch
        {
            "issuer" => CreateManualJwt(
                CloneJwtOptions(TestApplicationFactory.JwtOptions, issuer: "wrong-issuer"),
                DateTime.UtcNow.AddMinutes(30)),
            "audience" => CreateManualJwt(
                CloneJwtOptions(TestApplicationFactory.JwtOptions, audience: "wrong-audience"),
                DateTime.UtcNow.AddMinutes(30)),
            "signature" => CreateManualJwt(
                CloneJwtOptions(
                    TestApplicationFactory.JwtOptions,
                    signingKey: "wrong-signing-key-with-32-bytes-minimum"),
                DateTime.UtcNow.AddMinutes(30)),
            "expired" => CreateManualJwt(
                TestApplicationFactory.JwtOptions,
                DateTime.UtcNow.AddMinutes(-1)),
            _ => throw new InvalidOperationException("Unsupported test case.")
        };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/v1/auth-diagnostics/protected");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

}

