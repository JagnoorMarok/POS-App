namespace RestaurantManagement.Application.Common.Exceptions;

/// <summary>
/// Exception thrown when a requested domain entity is not found.
/// </summary>
public class NotFoundException : AppException
{
    public string EntityName { get; }
    public object Key { get; }

    public NotFoundException(string entityName, object key)
        : base($"Entity '{entityName}' with key '{key}' was not found.", "NOT_FOUND")
    {
        EntityName = entityName;
        Key = key;
    }
}
