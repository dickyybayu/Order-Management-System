namespace OMS.API.Tests.Unit;

public sealed class DevelopmentAdminSeederTests : TestBase
{
    [Fact]
    public void DevelopmentAdminUserCreationHashesPasswordAndAssignsAdminRole()
    {
        var passwordHasher = new BCryptPasswordHasher();
        var adminRoleId = Guid.NewGuid();
        var options = new DevelopmentAdminOptions
        {
            Enabled = true,
            Email = "Admin@Example.COM",
            FullName = " Admin User ",
            Password = "StrongPassword123!"
        };
        var normalizedEmail = User.NormalizeEmail(options.Email!);

        var user = DatabaseInitializer.CreateDevelopmentAdminUser(
            options,
            adminRoleId,
            normalizedEmail,
            passwordHasher);

        Assert.Equal("admin@example.com", user.Email);
        Assert.Equal("Admin User", user.FullName);
        Assert.Equal(adminRoleId, user.RoleId);
        Assert.True(user.IsActive);
        Assert.NotEqual(options.Password, user.PasswordHash);
        Assert.True(passwordHasher.VerifyPassword(options.Password!, user.PasswordHash));
    }

}

