using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using WildGoose.Domain;

namespace WildGoose.Filters;

public class GlobalExceptionFilter : IExceptionFilter
{
    private readonly ILogger<GlobalExceptionFilter> _logger;

    public GlobalExceptionFilter(ILogger<GlobalExceptionFilter> logger)
    {
        _logger = logger;
    }

    public void OnException(ExceptionContext context)
    {
        if (context.ExceptionHandled)
        {
            return;
        }

        if (context.Exception is WildGooseFriendlyException e)
        {
            context.Result = new BadRequestObjectResult(new
            {
                Success = false,
                Msg = e.Message,
                e.Code
            });
        }
        else
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Result =
                new ObjectResult(new
                {
                    Success = false,
                    Msg = "系统内部错误",
                    Code = StatusCodes.Status500InternalServerError,
                });
        }

        context.ExceptionHandled = true;
        _logger.LogError("{Exception}", context.Exception);
    }
}