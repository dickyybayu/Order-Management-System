using System.Text.Json.Serialization;

namespace OMS.API.Infrastructure.Integrations.Http.Frankfurter;

public sealed record FrankfurterLatestResponse(
    [property: JsonPropertyName("amount")] decimal Amount,
    [property: JsonPropertyName("base")] string? Base,
    [property: JsonPropertyName("date")] DateOnly? Date,
    [property: JsonPropertyName("rates")] IReadOnlyDictionary<string, decimal>? Rates);
