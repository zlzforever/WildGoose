using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using WildGoose.Domain;

namespace WildGoose.Filters;

public class GlobalExceptionFilter(ILogger<GlobalExceptionFilter> logger) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.ExceptionHandled)
        {
            return;
        }

        if (context.Exception is WildGooseFriendlyException e)
        {
            logger.LogError("{LogInfo} {Exception}", e.LogInfo, e);
            context.Result = new BadRequestObjectResult(new ApiResult
            {
                Success = false,
                Msg = e.Message,
                Code = e.Code
            });
        }
        else
        {
            logger.LogError("{Exception}", context.Exception);
            context.HttpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Result =
                new ObjectResult(new ApiResult
                {
                    Success = false,
                    Msg = "系统内部错误",
                    Code = StatusCodes.Status500InternalServerError,
                });
        }

        context.ExceptionHandled = true;
    }

    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
    class ApiResult
    {
        public int Code { get; set; }
        public bool Success { get; set; }
        public string? Msg { get; set; }
    }
}