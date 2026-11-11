namespace InventoryPro.API.Models;

/// <summary>Consistent JSON body returned for API errors.</summary>
public sealed record ApiErrorResponse(
    int Status,
    string Message,
    IReadOnlyDictionary<string, string[]> Errors)
{
    /// <summary>Creates an error response, defaulting to an empty field-error map.</summary>
    public static ApiErrorResponse Create(
        int status,
        string message,
        IReadOnlyDictionary<string, string[]>? errors = null) =>
        new(status, message, errors ?? new Dictionary<string, string[]>());
}
