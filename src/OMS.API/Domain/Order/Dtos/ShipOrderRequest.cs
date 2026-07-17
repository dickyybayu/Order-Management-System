using System.ComponentModel.DataAnnotations;

namespace OMS.API.Domain.Order.Dtos;

public sealed record ShipOrderRequest(
    [Required]
    [StringLength(100)]
    [RegularExpression(@".*\S.*", ErrorMessage = "TrackingNumber must not be blank.")]
    string TrackingNumber);
