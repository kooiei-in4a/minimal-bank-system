using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MinimalBankSystem.Api.Errors;

namespace MinimalBankSystem.Api.Filters;

public sealed class ApiModelStateFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (!context.ModelState.IsValid)
        {
            context.Result = new ObjectResult(ApiErrorEnvelope.ValidationFailed)
            {
                StatusCode = StatusCodes.Status400BadRequest,
            };
        }
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }
}
