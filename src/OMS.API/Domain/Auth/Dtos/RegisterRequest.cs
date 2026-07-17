using System.ComponentModel.DataAnnotations;
using OMS.API.Infrastructure.Shareds.Validators;

namespace OMS.API.Domain.Auth.Dtos;

public sealed class RegisterRequest
{
    public RegisterRequest(string email, string password, string fullName)
    {
        Email = email;
        Password = password;
        FullName = fullName;
    }

    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string Email { get; init; }

    [Required]
    [MinLength(AuthValidationRules.PasswordMinimumLength)]
    [RegularExpression(
        AuthValidationRules.PasswordPattern,
        ErrorMessage = AuthValidationRules.PasswordErrorMessage)]
    public string Password { get; init; }

    [Required]
    [MaxLength(AuthValidationRules.FullNameMaximumLength)]
    public string FullName { get; init; }
}
