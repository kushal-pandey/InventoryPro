using System.Net;
using System.Text.Json;
using FluentValidation;
using InventoryPro.API.Models;
using InventoryPro.Application.Common;

namespace InventoryPro.API.Middleware;

/// <summary>
/// Centralized exception handler so controllers stay free of try/catch blocks.
/// Converts domain exceptions (NotFoundException, ConflictException, FluentValidation
/// errors) into consistent JSON problem responses with the right HTTP status code.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    /// <summary>Creates the middleware with its downstream delegate and logger.</summary>
    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>Handles exceptions thrown by subsequent middleware and API handlers.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        ApiErrorResponse response;

        switch (exception)
        {
            case ValidationException validationEx:
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                response = ApiErrorResponse.Create(
                    context.Response.StatusCode,
                    "One or more validation errors occurred.",
                    validationEx.Errors
                        .GroupBy(error => error.PropertyName)
                        .ToDictionary(
                            group => group.Key,
                            group => group.Select(error => error.ErrorMessage).ToArray()));
                break;

            case ApiException apiEx:
                context.Response.StatusCode = apiEx.StatusCode;
                response = ApiErrorResponse.Create(apiEx.StatusCode, apiEx.Message);
                _logger.LogWarning(apiEx, "Handled API exception: {Message}", apiEx.Message);
                break;

            default:
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                response = ApiErrorResponse.Create(
                    context.Response.StatusCode,
                    "An unexpected error occurred. Please try again later.");
                _logger.LogError(exception, "Unhandled exception");
                break;
        }

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
    }
}
