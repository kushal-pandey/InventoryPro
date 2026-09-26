using System.Net;
using System.Text.Json;
using FluentValidation;
using InventoryPro.Application.Common;

namespace InventoryPro.API.Middleware;

/// <summary>
/// Centralized exception handler so controllers stay free of try/catch blocks.
/// Converts domain exceptions (NotFoundException, ConflictException, FluentValidation
/// errors) into consistent JSON problem responses with the right HTTP status code.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

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

        object response;

        switch (exception)
        {
            case ValidationException validationEx:
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                response = new
                {
                    status = context.Response.StatusCode,
                    message = "One or more validation errors occurred.",
                    errors = validationEx.Errors
                        .GroupBy(e => e.PropertyName)
                        .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
                };
                break;

            case ApiException apiEx:
                context.Response.StatusCode = apiEx.StatusCode;
                response = new { status = apiEx.StatusCode, message = apiEx.Message };
                _logger.LogWarning(apiEx, "Handled API exception: {Message}", apiEx.Message);
                break;

            default:
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                response = new { status = 500, message = "An unexpected error occurred. Please try again later." };
                _logger.LogError(exception, "Unhandled exception");
                break;
        }

        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}
