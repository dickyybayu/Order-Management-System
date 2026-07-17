namespace OMS.API.Models;

public sealed class Supplier : AuditableEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? Address { get; set; }

    public void TrimStringFieldsForStorage()
    {
        Name = Name.Trim();
        Email = TrimToNull(Email);
        Phone = TrimToNull(Phone);
        Address = TrimToNull(Address);
    }

    private static string? TrimToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
