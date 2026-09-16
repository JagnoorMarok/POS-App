namespace RestaurantManagement.Application.Common.Exceptions;

/// <summary>
/// Base exception for application-level domain and business logic errors.
/// </summary>
public class AppException : Exception
{
    public string? ErrorCode { get; }

    public AppException(string message, string? errorCode = null)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public AppException(string message, Exception innerException, string? errorCode = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}
