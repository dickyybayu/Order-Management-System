namespace OMS.API.Infrastructure.Exceptions;

public sealed class ForbiddenException : OmsException
{
    public ForbiddenException(string message)
        : base(message)
    {
    }
}
