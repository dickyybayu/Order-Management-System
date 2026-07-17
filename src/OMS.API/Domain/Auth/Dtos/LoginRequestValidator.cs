using System.ComponentModel.DataAnnotations;
using OMS.API.Domain.Auth.Dtos;

namespace OMS.API.Domain.Auth.Dtos;

public sealed class LoginRequestValidator
{
    public bool IsValid(LoginRequest request, out IReadOnlyCollection<ValidationResult> validationResults)
    {
        var results = new List<ValidationResult>();
        var validationContext = new ValidationContext(request);
        var isValid = Validator.TryValidateObject(
            request,
            validationContext,
            results,
            validateAllProperties: true);

        validationResults = results;

        return isValid;
    }
}
