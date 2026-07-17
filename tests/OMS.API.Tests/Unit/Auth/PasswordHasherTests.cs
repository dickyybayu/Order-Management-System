namespace OMS.API.Tests.Unit;

public sealed class PasswordHasherTests : TestBase
{
    [Fact]
    public void PasswordHasherHashesAndVerifiesPassword()
    {
        var passwordHasher = new BCryptPasswordHasher();
        const string password = "StrongPassword123!";

        var passwordHash = passwordHasher.HashPassword(password);

        Assert.NotEqual(password, passwordHash);
        Assert.True(passwordHasher.VerifyPassword(password, passwordHash));
        Assert.False(passwordHasher.VerifyPassword("WrongPassword123!", passwordHash));
    }

}

