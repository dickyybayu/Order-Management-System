using System.Net;
using System.Net.Http.Json;
using OMS.API.Infrastructure.Exceptions;
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

namespace OMS.API.Infrastructure.Integrations.Http.Frankfurter;

public sealed class FrankfurterExchangeRateClient(
    HttpClient httpClient,
    ILogger<FrankfurterExchangeRateClient> logger) : IExchangeRateClient
{
    public const string SourceName = "Frankfurter";

    public async Task<ExchangeRateResult> GetLatestRateAsync(
        string fromCurrency,
        string toCurrency,
        CancellationToken cancellationToken)
    {
        var normalizedFrom = CurrencyCode.Normalize(fromCurrency);
        var normalizedTo = CurrencyCode.Normalize(toCurrency);
        var requestUri = $"latest?from={Uri.EscapeDataString(normalizedFrom)}&to={Uri.EscapeDataString(normalizedTo)}";
        using var response = await SendAsync(requestUri, normalizedFrom, normalizedTo, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Frankfurter returned non-success status {StatusCode} for {FromCurrency}->{ToCurrency}",
                (int)response.StatusCode,
                normalizedFrom,
                normalizedTo);

            throw new ExternalServiceException("Currency exchange service is unavailable.");
        }

        FrankfurterLatestResponse? externalResponse;

        try
        {
            externalResponse = await response.Content.ReadFromJsonAsync<FrankfurterLatestResponse>(
                cancellationToken: cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or NotSupportedException or System.Text.Json.JsonException)
        {
            logger.LogWarning(
                exception,
                "Frankfurter returned malformed JSON for {FromCurrency}->{ToCurrency}",
                normalizedFrom,
                normalizedTo);
            throw new ExternalServiceException("Currency exchange service returned an invalid response.");
        }

        if (externalResponse?.Rates is null ||
            !externalResponse.Rates.TryGetValue(normalizedTo, out var rate) ||
            rate <= 0)
        {
            logger.LogWarning(
                "Frankfurter response did not contain a valid rate for {FromCurrency}->{ToCurrency}",
                normalizedFrom,
                normalizedTo);
            throw new ExternalServiceException("Currency exchange service returned an incomplete response.");
        }

        return new ExchangeRateResult(
            normalizedFrom,
            normalizedTo,
            rate,
            SourceName,
            externalResponse.Date,
            DateTime.UtcNow);
    }

    private async Task<HttpResponseMessage> SendAsync(
        string requestUri,
        string fromCurrency,
        string toCurrency,
        CancellationToken cancellationToken)
    {
        try
        {
            return await httpClient.GetAsync(requestUri, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(
                exception,
                "Frankfurter request failed for {FromCurrency}->{ToCurrency}",
                fromCurrency,
                toCurrency);
            throw new ExternalServiceException("Currency exchange service is unavailable.");
        }
    }
}
