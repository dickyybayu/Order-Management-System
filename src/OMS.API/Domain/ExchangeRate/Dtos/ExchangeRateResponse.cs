namespace OMS.API.Domain.ExchangeRate.Dtos;

public sealed record ExchangeRateResponse(
    string FromCurrency,
    string ToCurrency,
    decimal Rate,
    string Source,
    DateOnly? RateDate,
    DateTime RetrievedAtUtc);
