using System.ComponentModel.DataAnnotations;
using OMS.API.Domain.Auth.Dtos;
using OMS.API.Infrastructure.Shareds.Validators;

namespace OMS.API.Domain.User.Dtos;

public sealed record UpdateUserRequest(
    [Required]
    [EmailAddress]
    [MaxLength(255)]
    string Email,
    [Required]
    [MaxLength(AuthValidationRules.FullNameMaximumLength)]
    string FullName);
