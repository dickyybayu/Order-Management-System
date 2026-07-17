namespace OMS.API.Infrastructure.Integrations.Http.Frankfurter;

public sealed record ExchangeRateResult(
    string FromCurrency,
    string ToCurrency,
    decimal Rate,
    string Source,
    DateOnly? RateDate,
    DateTime RetrievedAtUtc);
