namespace OMS.API.Tests.Unit;

public sealed class JwtTokenServiceTests : TestBase
{
    [Fact]
    public void JwtOptionsValidateRequiredConfiguration()
    {
        var validOptions = CreateValidJwtOptions(expirationMinutes: 30);
        var missingSigningKey = new JwtOptions
        {
            Issuer = "issuer",
            Audience = "audience",
            ExpirationMinutes = 30
        };

        validOptions.Validate();
        Assert.Throws<InvalidOperationException>(missingSigningKey.Validate);
    }


    [Fact]
    public void TokenServiceCreatesJwtWithExpectedClaimsAndExpiration()
    {
        var options = CreateValidJwtOptions(expirationMinutes: 45);
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "admin@example.com",
            FullName = "Admin User"
        };
        var tokenService = new JwtTokenService(Options.Create(options));

        var tokenResult = tokenService.CreateAccessToken(user, SystemRoleNames.Admin);
        var principal = ValidateToken(tokenResult.AccessToken, options, out var validatedToken);

        Assert.Equal(user.Id.ToString(), principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal(user.Email, principal.FindFirstValue(ClaimTypes.Email));
        Assert.Equal(SystemRoleNames.Admin, principal.FindFirstValue(ClaimTypes.Role));
        Assert.Equal(user.FullName, principal.FindFirstValue(ClaimTypes.Name));
        Assert.True(tokenResult.ExpiresAtUtc > DateTime.UtcNow.AddMinutes(44));
        Assert.True(tokenResult.ExpiresAtUtc <= DateTime.UtcNow.AddMinutes(45).AddSeconds(5));
        Assert.Equal(tokenResult.ExpiresAtUtc, validatedToken.ValidTo, TimeSpan.FromSeconds(1));
    }

}

