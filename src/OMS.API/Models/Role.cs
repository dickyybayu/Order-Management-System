namespace OMS.API.Models;

public sealed class Role
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public ICollection<User> Users { get; } = new List<User>();
}
