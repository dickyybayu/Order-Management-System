namespace OMS.API.Infrastructure.Exceptions;

public sealed class NotFoundException : OmsException
{
    public NotFoundException(string message)
        : base(message)
    {
    }
}
