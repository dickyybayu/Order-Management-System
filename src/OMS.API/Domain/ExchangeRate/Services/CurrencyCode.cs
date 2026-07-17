using OMS.API.Infrastructure.Exceptions;

namespace OMS.API.Domain.ExchangeRate.Services;

public static class CurrencyCode
{
    public static string Normalize(string? currencyCode)
    {
        var normalized = currencyCode?.Trim().ToUpperInvariant() ?? string.Empty;

        if (normalized.Length != 3 || normalized.Any(character => character is < 'A' or > 'Z'))
        {
            throw new BusinessRuleException("Currency code must be exactly three alphabetic characters.");
        }

        return normalized;
    }
}
