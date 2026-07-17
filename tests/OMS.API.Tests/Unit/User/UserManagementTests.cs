namespace OMS.API.Tests.Unit;

public sealed class UserManagementTests : TestBase
{
    [Fact]
    public async Task UserManagementEndpointReturnsUnauthorizedWithoutToken()
    {
        await using var factory = new UserManagementApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }


    [Fact]
    public async Task UserManagementEndpointReturnsForbiddenForNonAdminRole()
    {
        await using var factory = new UserManagementApplicationFactory();
        using var client = factory.CreateClient();
        var user = CreateTokenUser(SystemRoleNames.SalesOperator);
        var token = new JwtTokenService(Options.Create(TestApplicationFactory.JwtOptions))
            .CreateAccessToken(user, SystemRoleNames.SalesOperator);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            token.AccessToken);

        var response = await client.GetAsync("/api/v1/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }


    [Fact]
    public async Task AdminCanListAndRetrieveUsers()
    {
        await using var factory = new UserManagementApplicationFactory();
        using var client = factory.CreateClient();
        var user = CreateTokenUser(SystemRoleNames.Admin);
        var token = new JwtTokenService(Options.Create(TestApplicationFactory.JwtOptions))
            .CreateAccessToken(user, SystemRoleNames.Admin);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            token.AccessToken);

        var listResponse = await client.GetAsync("/api/v1/users");
        var getResponse = await client.GetAsync($"/api/v1/users/{UserManagementApplicationFactory.UserId}");
        var listBody = await listResponse.Content.ReadAsStringAsync();
        var getBody = await getResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Contains("admin@example.com", listBody, StringComparison.Ordinal);
        Assert.Contains("admin@example.com", getBody, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(User.PasswordHash), listBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(nameof(User.PasswordHash), getBody, StringComparison.OrdinalIgnoreCase);
    }


    [Theory]
    [InlineData(SystemRoleNames.Admin)]
    [InlineData(SystemRoleNames.Supervisor)]
    [InlineData(SystemRoleNames.SalesOperator)]
    public async Task AdminCanCreateUsersWithSupportedRoles(string roleName)
    {
        var repository = new FakeUserRepository();
        var passwordHasher = new BCryptPasswordHasher();
        var service = CreateUserService(repository, passwordHasher: passwordHasher);
        var request = new CreateUserRequest(
            $"{roleName}@Example.COM",
            "StrongPassword123!",
            $"{roleName} User",
            roleName);

        var response = await service.CreateAsync(request, CancellationToken.None);
        var createdUser = Assert.Single(repository.Users);

        Assert.Equal(roleName, response.Role);
        Assert.Equal($"{roleName}@example.com", response.Email);
        Assert.Equal(repository.Roles.Single(role => role.Name == roleName).Id, createdUser.RoleId);
        Assert.NotEqual(request.Password, createdUser.PasswordHash);
        Assert.True(passwordHasher.VerifyPassword(request.Password, createdUser.PasswordHash));
    }


    [Fact]
    public async Task CreateUserRejectsDuplicateEmailWithConflictException()
    {
        var repository = new FakeUserRepository();
        repository.Users.Add(repository.CreateUser("sales@example.com", SystemRoleNames.SalesOperator));
        var service = CreateUserService(repository);

        await Assert.ThrowsAsync<ConflictException>(
            () => service.CreateAsync(
                new CreateUserRequest(
                    "SALES@example.com",
                    "StrongPassword123!",
                    "Sales User",
                    SystemRoleNames.SalesOperator),
                CancellationToken.None));
    }


    [Fact]
    public async Task UserResponseNeverReturnsPasswordHash()
    {
        var repository = new FakeUserRepository();
        var service = CreateUserService(repository);

        var response = await service.CreateAsync(
            new CreateUserRequest(
                "admin@example.com",
                "StrongPassword123!",
                "Admin User",
                SystemRoleNames.Admin),
            CancellationToken.None);
        var json = JsonSerializer.Serialize(response);

        Assert.DoesNotContain(nameof(User.PasswordHash), json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("StrongPassword123!", json, StringComparison.Ordinal);
    }


    [Fact]
    public async Task CreateUserRejectsUnknownRole()
    {
        var service = CreateUserService(new FakeUserRepository());

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.CreateAsync(
                new CreateUserRequest(
                    "user@example.com",
                    "StrongPassword123!",
                    "Unknown Role",
                    "owner"),
                CancellationToken.None));
    }


    [Fact]
    public async Task UserStatusUpdateActivatesAndDeactivatesUsers()
    {
        var repository = new FakeUserRepository();
        var user = repository.CreateUser("sales@example.com", SystemRoleNames.SalesOperator);
        repository.Users.Add(user);
        var service = CreateUserService(repository);

        var deactivated = await service.UpdateStatusAsync(
            user.Id,
            new UpdateUserStatusRequest(false),
            CancellationToken.None);
        var activated = await service.UpdateStatusAsync(
            user.Id,
            new UpdateUserStatusRequest(true),
            CancellationToken.None);

        Assert.False(deactivated.IsActive);
        Assert.True(activated.IsActive);
    }


    [Fact]
    public async Task UserRoleChangeWorksForSupportedRole()
    {
        var repository = new FakeUserRepository();
        var user = repository.CreateUser("sales@example.com", SystemRoleNames.SalesOperator);
        repository.Users.Add(user);
        var service = CreateUserService(repository);

        var response = await service.UpdateRoleAsync(
            user.Id,
            new UpdateUserRoleRequest(SystemRoleNames.Supervisor),
            CancellationToken.None);

        Assert.Equal(SystemRoleNames.Supervisor, response.Role);
        Assert.Equal(
            repository.Roles.Single(role => role.Name == SystemRoleNames.Supervisor).Id,
            user.RoleId);
    }


    [Fact]
    public async Task SelfDeactivationAndSelfRoleDemotionAreRejected()
    {
        var repository = new FakeUserRepository();
        var user = repository.CreateUser("admin@example.com", SystemRoleNames.Admin);
        repository.Users.Add(user);
        var service = CreateUserService(
            repository,
            currentUser: new FakeCurrentUserContext(user.Id, SystemRoleNames.Admin));

        await Assert.ThrowsAsync<ConflictException>(
            () => service.UpdateStatusAsync(
                user.Id,
                new UpdateUserStatusRequest(false),
                CancellationToken.None));
        await Assert.ThrowsAsync<ConflictException>(
            () => service.UpdateRoleAsync(
                user.Id,
                new UpdateUserRoleRequest(SystemRoleNames.Supervisor),
                CancellationToken.None));
    }


    [Fact]
    public async Task UserListSupportsPaginationSearchAndWhitelistedSorting()
    {
        var repository = new FakeUserRepository();
        repository.Users.Add(repository.CreateUser("zeta@example.com", SystemRoleNames.Admin, "Zeta User"));
        repository.Users.Add(repository.CreateUser("alpha@example.com", SystemRoleNames.Supervisor, "Alpha User"));
        repository.Users.Add(repository.CreateUser("sales@example.com", SystemRoleNames.SalesOperator, "Sales User"));
        var service = CreateUserService(repository);

        var response = await service.ListAsync(
            new UserListRequest
            {
                Search = "user",
                SortBy = "email",
                SortDirection = SortDirection.Asc,
                Page = 1,
                PageSize = 2
            },
            CancellationToken.None);

        Assert.Equal(3, response.Pagination.TotalItems);
        Assert.Equal(2, response.Items.Count);
        Assert.Equal(["alpha@example.com", "sales@example.com"], response.Items.Select(user => user.Email));
    }


    [Fact]
    public async Task UserListRejectsUnsupportedSortField()
    {
        var service = CreateUserService(new FakeUserRepository());

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.ListAsync(
                new UserListRequest { SortBy = "passwordHash" },
                CancellationToken.None));
    }

}

