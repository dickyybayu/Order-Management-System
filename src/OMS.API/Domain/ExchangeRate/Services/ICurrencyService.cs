using OMS.API.Domain.ExchangeRate.Dtos;

namespace OMS.API.Domain.ExchangeRate.Services;

public interface ICurrencyService
{
    Task<ExchangeRateResponse> GetExchangeRateAsync(
        string fromCurrency,
        string toCurrency,
        CancellationToken cancellationToken);
}
