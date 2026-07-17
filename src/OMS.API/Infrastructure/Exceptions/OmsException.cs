namespace OMS.API.Infrastructure.Exceptions;

public abstract class OmsException : Exception
{
    protected OmsException(string message)
        : base(message)
    {
    }
}
