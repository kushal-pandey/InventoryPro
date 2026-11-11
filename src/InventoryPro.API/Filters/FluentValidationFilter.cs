using FluentValidation;
using InventoryPro.API.Models;
using Microsoft.AspNetCore.Mvc.Filters;

namespace InventoryPro.API.Filters;

/// <summary>Runs any registered FluentValidation validator for controller action DTOs.</summary>
public sealed class FluentValidationFilter : IAsyncActionFilter
{
    /// <summary>Validates action DTO arguments and returns a consistent error response on failure.</summary>
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        var failures = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
                continue;

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (context.HttpContext.RequestServices.GetService(validatorType) is not IValidator validator)
                continue;

            var validationContext = new ValidationContext<object>(argument);
            var result = await validator.ValidateAsync(validationContext, context.HttpContext.RequestAborted);

            foreach (var error in result.Errors)
            {
                if (!failures.TryGetValue(error.PropertyName, out var messages))
                {
                    messages = new List<string>();
                    failures[error.PropertyName] = messages;
                }

                if (!messages.Contains(error.ErrorMessage, StringComparer.Ordinal))
                    messages.Add(error.ErrorMessage);
            }
        }

        if (failures.Count > 0)
        {
            context.Result = new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(
                ApiErrorResponse.Create(
                    StatusCodes.Status400BadRequest,
                    "One or more validation errors occurred.",
                    failures.ToDictionary(
                        entry => entry.Key,
                        entry => entry.Value.ToArray())));
            return;
        }

        await next();
    }
}
