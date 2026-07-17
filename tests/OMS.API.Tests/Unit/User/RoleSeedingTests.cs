namespace OMS.API.Tests.Unit;

public sealed class RoleSeedingTests : TestBase
{
    [Fact]
    public void SystemRoleNamesContainExactlyDocumentedRoles()
    {
        Assert.Equal(
            ["admin", "supervisor", "sales_operator"],
            SystemRoleNames.All);
    }


    [Fact]
    public void DatabaseInitializerCalculatesOnlyMissingSystemRoles()
    {
        var existingRoles = new HashSet<string>(StringComparer.Ordinal)
        {
            SystemRoleNames.Admin,
            SystemRoleNames.Supervisor
        };

        var missingRoles = DatabaseInitializer.GetMissingRoleNames(existingRoles);

        Assert.Equal([SystemRoleNames.SalesOperator], missingRoles);
    }


    [Fact]
    public void DatabaseInitializerRoleCalculationIsIdempotent()
    {
        var existingRoles = SystemRoleNames.All.ToHashSet(StringComparer.Ordinal);

        var missingRoles = DatabaseInitializer.GetMissingRoleNames(existingRoles);

        Assert.Empty(missingRoles);
    }


    [Fact]
    public void DevelopmentAdminOptionsRequireAllConfiguredValuesWhenEnabled()
    {
        var incompleteOptions = new DevelopmentAdminOptions
        {
            Enabled = true,
            Email = "admin@example.com",
            FullName = "Admin User"
        };
        var completeOptions = new DevelopmentAdminOptions
        {
            Enabled = true,
            Email = "admin@example.com",
            FullName = "Admin User",
            Password = "StrongPassword123!"
        };

        Assert.False(incompleteOptions.HasRequiredConfiguration());
        Assert.True(completeOptions.HasRequiredConfiguration());
    }


    [Fact]
    public void DevelopmentAdminEmailUsesExistingUserNormalizationStrategy()
    {
        var normalizedEmail = User.NormalizeEmail("  Admin@Example.COM  ");

        Assert.Equal("admin@example.com", normalizedEmail);
    }

}

