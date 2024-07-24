using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using WildGoose.Domain.Extensions;

namespace WildGoose;

public static class InvalidModelStateResponseFactory
{
    public static readonly Func<ActionContext, IActionResult> Instance = context =>
    {
        var errors = context.ModelState.Where(x =>
                x.Value?.ValidationState == ModelValidationState.Invalid)
            .Select(x => new ErrorDescriptor
            {
                Name = x.Key.ToCamelCase(),
                Messages = x.Value?.Errors.Where(z => !string.IsNullOrEmpty(z.ErrorMessage))
                    .Select(y => y.ErrorMessage).ToList()
            }).ToList();
        var logger = context.HttpContext.RequestServices
            .GetRequiredService<ILoggerFactory>().CreateLogger("InvalidModelStateResponseFactory");
        logger.LogDebug("Model state is invalid: {Errors}", JsonSerializer.Serialize(errors));
        return new ObjectResult(new
        {
            Code = 1,
            Success = false,
            Msg = "数据校验不通过",
            Errors = errors
        });
    };

    public class ErrorDescriptor
    {
        public string? Name { get; set; }
        public List<string>? Messages { get; set; }
    }
}