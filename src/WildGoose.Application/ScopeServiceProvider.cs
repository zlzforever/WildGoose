using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace WildGoose.Application;

public class ScopeServiceProvider(IHttpContextAccessor httpContextAccessor)
{
    public T GetService<T>()
    {
        return httpContextAccessor.HttpContext == null
            ? default
            : httpContextAccessor.HttpContext.RequestServices.GetService<T>();
    }
}