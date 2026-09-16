namespace RestaurantManagement.Application.Common.Exceptions;

/// <summary>
/// Exception thrown when business validation fails.
/// </summary>
public class ValidationException : AppException
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationException(string message)
        : base(message, "VALIDATION_FAILED")
    {
        Errors = new Dictionary<string, string[]>
        {
            { "General", new[] { message } }
        };
    }

    public ValidationException(string propertyName, string errorMessage)
        : base(errorMessage, "VALIDATION_FAILED")
    {
        Errors = new Dictionary<string, string[]>
        {
            { propertyName, new[] { errorMessage } }
        };
    }

    public ValidationException(IDictionary<string, string[]> errors, string message = "One or more validation failures have occurred.")
        : base(message, "VALIDATION_FAILED")
    {
        Errors = new Dictionary<string, string[]>(errors);
    }
}
