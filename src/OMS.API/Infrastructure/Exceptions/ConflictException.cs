namespace OMS.API.Infrastructure.Exceptions;

public sealed class ConflictException : OmsException
{
    public ConflictException(string message)
        : base(message)
    {
    }
}
