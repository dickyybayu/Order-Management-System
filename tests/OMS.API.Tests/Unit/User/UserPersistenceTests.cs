namespace OMS.API.Tests.Unit;

public sealed class UserPersistenceTests : TestBase
{
    [Fact]
    public void RoleAndUserEntitiesAreMappedAccordingToDatabaseDesign()
    {
        var dbContextOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost,1433;Database=OMS;TrustServerCertificate=True;")
            .Options;
        using var dbContext = new ApplicationDbContext(dbContextOptions);

        var roleEntity = dbContext.Model.FindEntityType(typeof(Role));
        var userEntity = dbContext.Model.FindEntityType(typeof(User));

        Assert.NotNull(roleEntity);
        Assert.NotNull(userEntity);
        Assert.Equal("Roles", roleEntity.GetTableName());
        Assert.Equal("Users", userEntity.GetTableName());
        Assert.Equal(50, roleEntity.FindProperty(nameof(Role.Name))?.GetMaxLength());
        Assert.False(roleEntity.FindProperty(nameof(Role.Name))?.IsNullable);
        Assert.Equal(255, userEntity.FindProperty(nameof(User.Email))?.GetMaxLength());
        Assert.Equal(255, userEntity.FindProperty(nameof(User.PasswordHash))?.GetMaxLength());
        Assert.Equal(150, userEntity.FindProperty(nameof(User.FullName))?.GetMaxLength());
        Assert.False(userEntity.FindProperty(nameof(User.Email))?.IsNullable);
        Assert.False(userEntity.FindProperty(nameof(User.PasswordHash))?.IsNullable);
        Assert.False(userEntity.FindProperty(nameof(User.FullName))?.IsNullable);
        Assert.False(userEntity.FindProperty(nameof(User.IsActive))?.IsNullable);
        Assert.False(userEntity.FindProperty(nameof(User.CreatedAtUtc))?.IsNullable);
    }


    [Fact]
    public void RoleAndUserEntitiesHaveRequiredIndexesAndRelationship()
    {
        var dbContextOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost,1433;Database=OMS;TrustServerCertificate=True;")
            .Options;
        using var dbContext = new ApplicationDbContext(dbContextOptions);

        var roleEntity = dbContext.Model.FindEntityType(typeof(Role));
        var userEntity = dbContext.Model.FindEntityType(typeof(User));

        Assert.NotNull(roleEntity);
        Assert.NotNull(userEntity);
        Assert.Contains(
            roleEntity.GetIndexes(),
            index => index.IsUnique && index.Properties.Single().Name == nameof(Role.Name));
        Assert.Contains(
            userEntity.GetIndexes(),
            index => index.IsUnique && index.Properties.Single().Name == nameof(User.Email));

        var roleRelationship = Assert.Single(userEntity.GetForeignKeys());
        Assert.Equal(nameof(User.RoleId), roleRelationship.Properties.Single().Name);
        Assert.Equal(typeof(Role), roleRelationship.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Restrict, roleRelationship.DeleteBehavior);
    }


    [Fact]
    public void UserEmailIsNormalizedBeforeStorage()
    {
        var user = new User
        {
            Email = "  Sales.Operator@Example.COM  "
        };

        user.NormalizeEmailForStorage();

        Assert.Equal("sales.operator@example.com", user.Email);
    }


    [Fact]
    public void PasswordHashIsNotExposedByResponseModels()
    {
        var responseProperties = typeof(ApiResponse).Assembly.GetTypes()
            .Where(type => type.Name.EndsWith("Response", StringComparison.Ordinal))
            .SelectMany(type => type.GetProperties());

        Assert.DoesNotContain(
            responseProperties,
            property => property.Name.Equals(nameof(User.PasswordHash), StringComparison.OrdinalIgnoreCase));
    }

}

