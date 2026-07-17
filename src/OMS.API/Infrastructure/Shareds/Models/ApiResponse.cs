namespace OMS.API.Infrastructure.Shareds.Models;

public sealed record ApiResponse<T>(
    bool Success,
    string Message,
    T? Data)
{
    public static ApiResponse<T> Ok(T data, string message) => new(true, message, data);
}

public sealed record ApiResponse(
    bool Success,
    string Message)
{
    public static ApiResponse Ok(string message) => new(true, message);
}
