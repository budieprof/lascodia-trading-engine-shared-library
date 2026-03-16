using Microsoft.AspNetCore.Http;
using Lascodia.Trading.Engine.SharedApplication.Common.Exceptions;
using Lascodia.Trading.Engine.SharedApplication.Common.Models;
using Lascodia.Trading.Engine.SharedLibrary;

namespace Lascodia.Trading.Engine.SharedApplication.Common.ExceptionHandlers;

public class ValidationExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;

    public ValidationExceptionHandlerMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidationException ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, ValidationException exception)
    {
        Console.WriteLine($"Exception: {exception}");
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(Helper.GetJson(ResponseData<string>.Init("0", false, exception.Message, "-01")));
    }
}
