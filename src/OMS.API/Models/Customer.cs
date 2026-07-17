namespace OMS.API.Models;

public sealed class Customer : AuditableEntity
{
    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string ShippingAddress { get; set; } = string.Empty;

    public void NormalizeForStorage()
    {
        Name = Name.Trim();
        Email = User.NormalizeEmail(Email);
        Phone = string.IsNullOrWhiteSpace(Phone) ? null : Phone.Trim();
        ShippingAddress = ShippingAddress.Trim();
    }
}
