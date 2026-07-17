using System.ComponentModel.DataAnnotations;

namespace OMS.API.Infrastructure.Shareds.Validators;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class PositiveDecimalAttribute : ValidationAttribute
{
    public PositiveDecimalAttribute()
        : base("The field {0} must be greater than zero.")
    {
    }

    public override bool IsValid(object? value)
    {
        return value is decimal decimalValue && decimalValue > 0;
    }
}
