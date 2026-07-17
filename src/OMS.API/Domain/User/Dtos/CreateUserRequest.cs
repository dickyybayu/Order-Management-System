using System.ComponentModel.DataAnnotations;
using OMS.API.Domain.Auth.Dtos;
using OMS.API.Infrastructure.Shareds.Validators;

namespace OMS.API.Domain.User.Dtos;

public sealed record CreateUserRequest(
    [Required]
    [EmailAddress]
    [MaxLength(255)]
    string Email,
    [Required]
    [MinLength(AuthValidationRules.PasswordMinimumLength)]
    [RegularExpression(
        AuthValidationRules.PasswordPattern,
        ErrorMessage = AuthValidationRules.PasswordErrorMessage)]
    string Password,
    [Required]
    [MaxLength(AuthValidationRules.FullNameMaximumLength)]
    string FullName,
    [Required]
    [MaxLength(50)]
    string Role);
