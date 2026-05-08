using Serilog.Core;
using Serilog.Events;

namespace WildGoose.Serilog;

/// <summary>
/// 前端调用必须这么传
/// traceparent: 00-9ebccfc4e0ad27a2aeb8d451f415abc9-0000000000000000-00
/// </summary>
public class WildGooseLogEventEnricher : ILogEventEnricher
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    ///
    /// </summary>
    public WildGooseLogEventEnricher()
    {
        _httpContextAccessor = new HttpContextAccessor();
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="logEvent"></param>
    /// <param name="propertyFactory"></param>
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        // 非 HTTP 日志也需要补充

        // 应该放在 OTEL 的 Resource attribute 中
        // AddScalarProperty(logEvent, "host_ip", Defaults.IPAddress);
        // AddScalarProperty(logEvent, "host_name", Defaults.HostName);
        // AddScalarProperty(logEvent, "environment", Defaults.Environment);

        if (_httpContextAccessor.HttpContext == null)
        {
            return;
        }

        // 00-xxx-yyy-01
        // 
        var context = _httpContextAccessor.HttpContext;
        var request = context.Request;
        var headers = request.Headers;
        // ip info
        var ip = GetRemoteIpAddressString(_httpContextAccessor.HttpContext);
        AddScalarProperty(logEvent, "remote_addr", ip);

        // 对于 3 个字符串的拼接，StringBuilder 比 string.Concat 多了至少 2 次额外操作，性能反而下降
        // 小量拼接（<5 个固定字符串）：优先用插值 /Concat，可读性 > 微优化
        // 只有当拼接次数多、字符串长度不确定时，StringBuilder 才体现优势
        // 大量 / 循环拼接：必须用 StringBuilder，且建议指定初始容量（减少扩容）
        // AddScalarProperty(logEvent, "request", $"{request.Method} {request.Path} {request.Protocol}");
        AddScalarProperty(logEvent, "request_method", request.Method);
        if (!string.IsNullOrEmpty(request.QueryString.Value))
        {
            AddScalarProperty(logEvent, "query_string", request.QueryString.Value);
        }

        // userinfo
        AddHeaderProperty(logEvent, headers, "user_id", "z-user-id");
        AddHeaderProperty(logEvent, headers, "user_name", "z-user-name");

        // system info
        // AddHeaderProperty(logEvent, headers, "frontend_version", "z-frontend-version");
        AddHeaderProperty(logEvent, headers, "application_id", "z-application-id");
        AddHeaderProperty(logEvent, headers, "device_id", "z-device-id");
        AddHeaderProperty(logEvent, headers, "os", "z-os");
        AddHeaderProperty(logEvent, headers, "app_id", "z-app-id");
        AddHeaderProperty(logEvent, headers, "imei", "z-imei");
        AddHeaderProperty(logEvent, headers, "alt", "z-alt");
        AddHeaderProperty(logEvent, headers, "lat", "z-lat");
        AddHeaderProperty(logEvent, headers, "lon", "z-lon");
        AddHeaderProperty(logEvent, headers, "platform", "z-platform");

        logEvent.AddOrUpdateProperty(new LogEventProperty("request_uri", new ScalarValue(request.Path)));

        AddHeaderProperty(logEvent, headers, "protocol", request.Protocol);

        logEvent.RemovePropertyIfPresent("RequestPath");
        logEvent.RemovePropertyIfPresent("ConnectionId");
        logEvent.RemovePropertyIfPresent("ActionId");
        logEvent.RemovePropertyIfPresent("RequestId");
    }

    public string GetRemoteIpAddressString(HttpContext httpContext)
    {
        var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (string.IsNullOrEmpty(forwardedFor))
        {
            forwardedFor = httpContext.Connection.RemoteIpAddress?.ToString();
        }

        return forwardedFor ?? "";
    }

    private void AddScalarProperty(LogEvent logEvent, string propertyName, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        logEvent.AddPropertyIfAbsent(new LogEventProperty(propertyName, new ScalarValue(value)));
    }

    private void AddHeaderProperty(LogEvent logEvent, IHeaderDictionary headers, string propertyName,
        params string[] headerNames)
    {
        foreach (var headerName in headerNames)
        {
            if (!headers.ContainsKey(headerName))
            {
                continue;
            }

            var value = headers[headerName].ToString();
            if (!string.IsNullOrEmpty(value))
            {
                logEvent.AddPropertyIfAbsent(
                    new LogEventProperty(propertyName, new ScalarValue(value)));
                break;
            }
        }
    }
}