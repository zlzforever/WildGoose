using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace WildGoose.Filters;

public sealed class ResponseWrapperFilter : IAsyncResultFilter
{
    private readonly ILogger<ResponseWrapperFilter> _logger;

    public ResponseWrapperFilter(ILogger<ResponseWrapperFilter> logger)
    {
        _logger = logger;
    }

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        _logger.LogDebug("开始执行返回结果过滤器");
        _logger.LogInformation($"Headers: {JsonSerializer.Serialize(context.HttpContext.Request.Headers)}");

        // dapr 调用不包装 APIResult
        var invokeFromDapr = !string.IsNullOrEmpty(context.HttpContext.Request.Headers["Pubsubname"])
                             || !string.IsNullOrEmpty(context.HttpContext.Request.Headers["Dapr-App-Id"]);
        if (invokeFromDapr)
        {
            await next();
            return;
        }

        // 服务调用不做 APIResult 包装
        if (context.HttpContext.Request.Headers.TryGetValue("service-invocation", out var value))
        {
            if ("true".Equals(value, StringComparison.OrdinalIgnoreCase))
            {
                await next();
                return;
            }
        }

        // comments by lewis at 20240103
        // 只能使用 type 比较， 不能使用 is， 不然如 BadRequestObjectResult 也会被二次包装
        if (context.Result.GetType() == typeof(ObjectResult))
        {
            var objectResult = (ObjectResult)context.Result;
            if (objectResult.Value is ProblemDetails problemDetails)
            {
                var num = problemDetails.Status ?? 200;
                var success = IsSuccessStatusCode(num);
                if (success)
                {
                    context.Result = new ObjectResult(new { Code = 0, Data = objectResult.Value, Msg = string.Empty });
                }
                else
                {
                    context.Result = new ObjectResult(new
                    {
                        Success = IsSuccessStatusCode(num),
                        Code = -1,
                        Msg = problemDetails.Title ?? string.Empty
                    })
                    {
                        StatusCode = num
                    };
                }
            }
            else
            {
                context.Result = new ObjectResult(new { Code = 0, Data = objectResult.Value, Msg = string.Empty });
            }
        }
        else if (context.Result is EmptyResult)
        {
            context.Result = new ObjectResult(new
            {
                Success = true,
                Code = 0,
                Msg = string.Empty
            });
        }

        _logger.LogDebug("执行返回结果过滤器结束");
        await next();
    }

    static bool IsSuccessStatusCode(int statusCode)
    {
        return statusCode is >= 200 and <= 299;
    }
}