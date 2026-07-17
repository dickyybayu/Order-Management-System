namespace OMS.API.Infrastructure.Exceptions;

public sealed class BusinessRuleException : OmsException
{
    public BusinessRuleException(string message)
        : base(message)
    {
    }
}
