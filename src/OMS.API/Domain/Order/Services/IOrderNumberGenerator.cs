namespace OMS.API.Domain.Order.Services;

public interface IOrderNumberGenerator
{
    string Create(DateTime createdAtUtc);
}
