using System.ComponentModel.DataAnnotations;

namespace OMS.API.Domain.Order.Dtos;

public sealed class CreateOrderRequest : IValidatableObject
{
    public CreateOrderRequest(
        Guid customerId,
        string currencyCode,
        IReadOnlyCollection<CreateOrderItemRequest> items)
    {
        CustomerId = customerId;
        CurrencyCode = currencyCode;
        Items = items;
    }

    public Guid CustomerId { get; init; }

    [Required]
    [RegularExpression("^[A-Za-z]{3}$", ErrorMessage = "CurrencyCode must contain exactly 3 letters.")]
    public string CurrencyCode { get; init; }

    [Required]
    [MinLength(1)]
    public IReadOnlyCollection<CreateOrderItemRequest> Items { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (CustomerId == Guid.Empty)
        {
            yield return new ValidationResult(
                "CustomerId is required.",
                [nameof(CustomerId)]);
        }
    }
}

public sealed class CreateOrderItemRequest : IValidatableObject
{
    public CreateOrderItemRequest(Guid productId, int quantity)
    {
        ProductId = productId;
        Quantity = quantity;
    }

    public Guid ProductId { get; init; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ProductId == Guid.Empty)
        {
            yield return new ValidationResult(
                "ProductId is required.",
                [nameof(ProductId)]);
        }
    }
}
