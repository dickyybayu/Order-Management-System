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

namespace OMS.API.Domain.Order.Services;

public sealed class OrderNumberGenerator : IOrderNumberGenerator
{
    public string Create(DateTime createdAtUtc)
    {
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        return $"ORD-{createdAtUtc:yyyyMMdd}-{uniqueSuffix}";
    }
}
