using OMS.API.Domain.ExchangeRate.Dtos;
using OMS.API.Infrastructure.Integrations.Http.Frankfurter;
using OMS.API.Domain.Auth.Services;
using OMS.API.Domain.Auth.Token;
using OMS.API.Domain.Category.Services;
using OMS.API.Domain.Customer.Services;
using OMS.API.Domain.ExchangeRate.Services;
using OMS.API.Domain.Order.Services;
using OMS.API.Domain.Product.Services;
using OMS.API.Domain.Reporting.Services;
using OMS.API.Domain.Supplier.Services;
using OMS.API.Domain.User.Services;

namespace OMS.API.Domain.ExchangeRate.Services;

public sealed class CurrencyService(IExchangeRateClient exchangeRateClient) : ICurrencyService
{
    public Task<ExchangeRateResponse> GetExchangeRateAsync(
        string fromCurrency,
        string toCurrency,
        CancellationToken cancellationToken)
    {
        var normalizedFrom = CurrencyCode.Normalize(fromCurrency);
        var normalizedTo = CurrencyCode.Normalize(toCurrency);

        if (normalizedFrom == normalizedTo)
        {
            return Task.FromResult(new ExchangeRateResponse(
                normalizedFrom,
                normalizedTo,
                1m,
                "Identity",
                DateOnly.FromDateTime(DateTime.UtcNow),
                DateTime.UtcNow));
        }

        return GetExternalRateAsync(normalizedFrom, normalizedTo, cancellationToken);
    }

    private async Task<ExchangeRateResponse> GetExternalRateAsync(
        string normalizedFrom,
        string normalizedTo,
        CancellationToken cancellationToken)
    {
        var result = await exchangeRateClient.GetLatestRateAsync(normalizedFrom, normalizedTo, cancellationToken);

        return new ExchangeRateResponse(
            result.FromCurrency,
            result.ToCurrency,
            result.Rate,
            result.Source,
            result.RateDate,
            result.RetrievedAtUtc);
    }
}
