namespace OMS.API.Models;

public sealed class Category : AuditableEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public void NormalizeNameForStorage()
    {
        Name = NormalizeName(Name);
    }

    public static string NormalizeName(string name)
    {
        return name.Trim();
    }
}
