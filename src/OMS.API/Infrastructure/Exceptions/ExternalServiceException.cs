namespace OMS.API.Infrastructure.Exceptions;

public sealed class ExternalServiceException : OmsException
{
    public ExternalServiceException(string message)
        : base(message)
    {
    }
}
