using Lascodia.Trading.Engine.SharedApplication.Common.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace Lascodia.Trading.Engine.SharedAPI.Filters;

public class GlobalExceptionFilter : IExceptionFilter
{
    private readonly ILogger<GlobalExceptionFilter> _logger;

    public GlobalExceptionFilter(ILogger<GlobalExceptionFilter> logger) => _logger = logger;

    public void OnException(ExceptionContext context)
    {
        _logger.LogError(context.Exception, "Unhandled exception: {Message}", context.Exception.Message);

        switch (context.Exception)
        {
            case ValidationException validationException:
                context.Result = new BadRequestObjectResult(new
                {
                    title = "Validation Failed",
                    status = StatusCodes.Status400BadRequest,
                    errors = validationException.Errors
                });
                context.ExceptionHandled = true;
                break;

            case NotFoundException notFoundException:
                context.Result = new NotFoundObjectResult(new
                {
                    title = "Not Found",
                    status = StatusCodes.Status404NotFound,
                    detail = notFoundException.Message
                });
                context.ExceptionHandled = true;
                break;

            case ForbiddenAccessException:
                context.Result = new ForbidResult();
                context.ExceptionHandled = true;
                break;
        }
    }
}
