namespace InventoryPro.Application.Common;

public class ApiException : Exception
{
    public int StatusCode { get; }

    public ApiException(string message, int statusCode = 400) : base(message)
    {
        StatusCode = statusCode;
    }
}

public class NotFoundException : ApiException
{
    public NotFoundException(string entityName, object key)
        : base($"{entityName} with id '{key}' was not found.", 404)
    {
    }
}

public class ConflictException : ApiException
{
    public ConflictException(string message) : base(message, 409)
    {
    }
}
