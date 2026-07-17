namespace OMS.API.Tests.Unit;

public sealed class AuthServiceTests : TestBase
{
    [Fact]
    public void ApiFoundationServicesCanBeRegistered()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddApiFoundation();

        using var serviceProvider = services.BuildServiceProvider();

        Assert.NotNull(serviceProvider.GetService<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckService>());
    }


    [Fact]
    public void PersistenceServicesRegisterApplicationDbContextWithSqlServer()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=localhost,1433;Database=OMS;TrustServerCertificate=True;"
            })
            .Build();

        services.AddLogging();
        services.AddPersistence(configuration);
        services.AddOmsAuthenticationServices(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.True(dbContext.Database.IsSqlServer());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>());
    }


    [Fact]
    public async Task RegisterCreatesSalesOperatorAndReturnsUserResponse()
    {
        var repository = new FakeAuthRepository();
        var passwordHasher = new BCryptPasswordHasher();
        var service = CreateAuthService(repository, passwordHasher);
        var request = new RegisterRequest(
            "  Sales.Operator@Example.COM  ",
            "StrongPassword123!",
            " Sales Operator ");

        var response = await service.RegisterAsync(request, CancellationToken.None);
        var createdUser = Assert.Single(repository.Users);

        Assert.Equal("sales.operator@example.com", response.Email);
        Assert.Equal("Sales Operator", response.FullName);
        Assert.Equal(SystemRoleNames.SalesOperator, response.Role);
        Assert.Equal(repository.SalesOperatorRole.Id, createdUser.RoleId);
        Assert.True(createdUser.IsActive);
        Assert.NotEqual(request.Password, createdUser.PasswordHash);
        Assert.True(passwordHasher.VerifyPassword(request.Password, createdUser.PasswordHash));
    }


    [Fact]
    public async Task RegisterRejectsDuplicateEmailWithConflictException()
    {
        var repository = new FakeAuthRepository();
        repository.Users.Add(new User
        {
            Email = "sales@example.com",
            PasswordHash = "existing-hash",
            FullName = "Existing User",
            RoleId = repository.SalesOperatorRole.Id,
            Role = repository.SalesOperatorRole
        });
        var service = CreateAuthService(repository);
        var request = new RegisterRequest("SALES@example.com", "StrongPassword123!", "Sales User");

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => service.RegisterAsync(request, CancellationToken.None));

        Assert.Contains("already registered", exception.Message, StringComparison.OrdinalIgnoreCase);
    }


    [Fact]
    public async Task RegisterControllerReturnsCreatedStatus()
    {
        var userResponse = new AuthUserResponse(
            Guid.NewGuid(),
            "sales@example.com",
            "Sales User",
            SystemRoleNames.SalesOperator);
        var controller = new AuthController(new FakeAuthService(registerResponse: userResponse));

        var actionResult = await controller.Register(
            new RegisterRequest("sales@example.com", "StrongPassword123!", "Sales User"),
            CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(actionResult.Result);
        var apiResponse = Assert.IsType<ApiResponse<AuthUserResponse>>(objectResult.Value);
        Assert.Equal(StatusCodes.Status201Created, objectResult.StatusCode);
        Assert.True(apiResponse.Success);
        Assert.Same(userResponse, apiResponse.Data);
    }


    [Fact]
    public async Task ValidLoginReturnsJwtAndExpectedUserData()
    {
        var repository = new FakeAuthRepository();
        var passwordHasher = new BCryptPasswordHasher();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "sales@example.com",
            FullName = "Sales Operator",
            PasswordHash = passwordHasher.HashPassword("StrongPassword123!"),
            RoleId = repository.SalesOperatorRole.Id,
            Role = repository.SalesOperatorRole,
            IsActive = true
        };
        repository.Users.Add(user);
        var jwtOptions = CreateValidJwtOptions(expirationMinutes: 30);
        var service = new AuthService(
            repository,
            passwordHasher,
            new JwtTokenService(Options.Create(jwtOptions)));

        var response = await service.LoginAsync(
            new LoginRequest("SALES@example.com", "StrongPassword123!"),
            CancellationToken.None);
        var principal = ValidateToken(response.AccessToken, jwtOptions, out _);

        Assert.False(string.IsNullOrWhiteSpace(response.AccessToken));
        Assert.Equal(user.Id, response.User.Id);
        Assert.Equal(user.Email, response.User.Email);
        Assert.Equal(user.FullName, response.User.FullName);
        Assert.Equal(SystemRoleNames.SalesOperator, response.User.Role);
        Assert.Equal(user.Id.ToString(), principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal(SystemRoleNames.SalesOperator, principal.FindFirstValue(ClaimTypes.Role));
    }


    [Theory]
    [InlineData("missing@example.com", "StrongPassword123!")]
    [InlineData("sales@example.com", "WrongPassword123!")]
    public async Task InvalidLoginReturnsUnauthorizedWithGenericMessage(string email, string password)
    {
        var repository = new FakeAuthRepository();
        var passwordHasher = new BCryptPasswordHasher();
        repository.Users.Add(new User
        {
            Email = "sales@example.com",
            FullName = "Sales Operator",
            PasswordHash = passwordHasher.HashPassword("StrongPassword123!"),
            RoleId = repository.SalesOperatorRole.Id,
            Role = repository.SalesOperatorRole,
            IsActive = true
        });
        var service = CreateAuthService(repository, passwordHasher);

        var exception = await Assert.ThrowsAsync<UnauthorizedException>(
            () => service.LoginAsync(new LoginRequest(email, password), CancellationToken.None));

        Assert.Equal(AuthService.InvalidCredentialsMessage, exception.Message);
    }


    [Fact]
    public async Task InactiveUserCannotLogin()
    {
        var repository = new FakeAuthRepository();
        var passwordHasher = new BCryptPasswordHasher();
        repository.Users.Add(new User
        {
            Email = "sales@example.com",
            FullName = "Sales Operator",
            PasswordHash = passwordHasher.HashPassword("StrongPassword123!"),
            RoleId = repository.SalesOperatorRole.Id,
            Role = repository.SalesOperatorRole,
            IsActive = false
        });
        var service = CreateAuthService(repository, passwordHasher);

        var exception = await Assert.ThrowsAsync<UnauthorizedException>(
            () => service.LoginAsync(
                new LoginRequest("sales@example.com", "StrongPassword123!"),
                CancellationToken.None));

        Assert.Equal(AuthService.InvalidCredentialsMessage, exception.Message);
    }


    [Fact]
    public void AuthResponseModelsNeverExposePasswordHash()
    {
        var response = new AuthResponse(
            "access-token",
            DateTime.UtcNow.AddMinutes(30),
            new AuthUserResponse(Guid.NewGuid(), "sales@example.com", "Sales User", SystemRoleNames.SalesOperator));

        var json = JsonSerializer.Serialize(response);

        Assert.DoesNotContain(nameof(User.PasswordHash), json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hash", json, StringComparison.OrdinalIgnoreCase);
    }


    [Fact]
    public void RegisterRequestValidatorEnforcesDocumentedPasswordStrength()
    {
        var request = new RegisterRequest("sales@example.com", "weak", "Sales User");
        var validator = new RegisterRequestValidator();

        var isValid = validator.IsValid(request, out var validationResults);

        Assert.False(isValid);
        Assert.Contains(
            validationResults,
            result => string.Equals(
                result.ErrorMessage,
                AuthValidationRules.PasswordErrorMessage,
                StringComparison.Ordinal));
    }

}

