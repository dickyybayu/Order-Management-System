namespace OMS.API.Infrastructure.Configurations;

public sealed class DevelopmentAdminOptions
{
    public const string SectionName = "DevelopmentAdmin";

    public bool Enabled { get; init; }

    public string? Email { get; init; }

    public string? FullName { get; init; }

    public string? Password { get; init; }

    public bool HasRequiredConfiguration()
    {
        return !string.IsNullOrWhiteSpace(Email)
            && !string.IsNullOrWhiteSpace(FullName)
            && !string.IsNullOrWhiteSpace(Password);
    }
}
