using System.ComponentModel.DataAnnotations;

namespace OMS.API.Infrastructure.Integrations.Http.Frankfurter;

public sealed class FrankfurterOptions
{
    public const string SectionName = "Frankfurter";

    public string BaseUrl { get; init; } = "https://api.frankfurter.app/";

    [Range(1, 300)]
    public int TimeoutSeconds { get; init; } = 10;

    [Range(0, 10)]
    public int RetryCount { get; init; } = 3;

    [Range(1, 20)]
    public int CircuitBreakerFailureThreshold { get; init; } = 3;

    [Range(1, 300)]
    public int CircuitBreakerDurationSeconds { get; init; } = 30;
}
