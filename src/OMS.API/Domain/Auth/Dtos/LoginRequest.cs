using System.ComponentModel.DataAnnotations;

namespace OMS.API.Domain.Auth.Dtos;

public sealed class LoginRequest
{
    public LoginRequest(string email, string password)
    {
        Email = email;
        Password = password;
    }

    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string Email { get; init; }

    [Required]
    public string Password { get; init; }
}
