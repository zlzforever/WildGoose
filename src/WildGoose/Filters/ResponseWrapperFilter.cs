using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace WildGoose.Filters;

public sealed class ResponseWrapperFilter : ActionFilterAttribute
{
    public override void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is BadRequestObjectResult)
        {
            return;
        }
        // 根据请求内容的类型，返回对应的空结果
        // 比如请求的是一个图片，那么返回一个空的图片
        if (context.Result is ObjectResult objectResult)
        {
            context.Result = new ObjectResult(new
                { Code = 0, Success = true, Msg = string.Empty, Data = objectResult.Value });
        }
    }
}