using System.ComponentModel.DataAnnotations;

namespace OMS.API.Domain.Order.Services;

public sealed class OrderCurrencyOptions
{
    public const string SectionName = "OrderCurrency";

    [Required]
    [RegularExpression("^[A-Za-z]{3}$")]
    public string BaseCurrencyCode { get; init; } = "IDR";
}
