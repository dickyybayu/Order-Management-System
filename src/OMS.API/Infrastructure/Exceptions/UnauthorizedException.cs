namespace OMS.API.Infrastructure.Exceptions;

public sealed class UnauthorizedException : OmsException
{
    public UnauthorizedException(string message)
        : base(message)
    {
    }
}
