using System.ComponentModel.DataAnnotations;

namespace OMS.API.Domain.Order.Dtos;

public sealed record CancelOrderRequest(
    [Required]
    [StringLength(500)]
    [RegularExpression(@".*\S.*", ErrorMessage = "Reason must not be blank.")]
    string Reason);
