namespace OMS.API.Infrastructure.Integrations.Http.Frankfurter;

public interface IExchangeRateClient
{
    Task<ExchangeRateResult> GetLatestRateAsync(
        string fromCurrency,
        string toCurrency,
        CancellationToken cancellationToken);
}
