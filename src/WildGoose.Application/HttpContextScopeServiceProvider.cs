using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using WildGoose.Infrastructure;

namespace WildGoose.Application;

public class HttpContextScopeServiceProvider(IHttpContextAccessor httpContextAccessor)
    : ScopeServiceProvider
{
    public override T GetService<T>()
    {
        return httpContextAccessor.HttpContext == null
            ? default
            : httpContextAccessor.HttpContext.RequestServices.GetService<T>();
    }
}