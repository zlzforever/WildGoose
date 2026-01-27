using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using WildGoose.Application;

namespace WildGoose.Filters;

public class TokenAuthHandler(IOptionsMonitor<TokenAuthOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<TokenAuthOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var actionContext = Context.GetEndpoint();
        if (actionContext == null)
        {
            return AuthenticateResult.Fail("No endpoint found");
        }

        if (string.IsNullOrEmpty(Options.SecurityToken))
        {
            return AuthenticateResult.Fail("No security token");
        }

        var token = Context.Request.Headers["X-AUTH-TOKEN"].ToString();

        if (string.IsNullOrEmpty(token))
        {
            Logger.LogError("认证码未提供 {TraceId}", Context.TraceIdentifier);
            return AuthenticateResult.Fail("401");
        }

        if (token != Options.SecurityToken)
        {
            Logger.LogError("认证码匹配失败 {TraceId} {Expected} {Actual}", Context.TraceIdentifier, Options.SecurityToken,
                token);
            return AuthenticateResult.Fail("401");
        }

        var claims = new List<Claim>
        {
            new("sub", "SecurityToken"),
            new(ClaimTypes.Name, "SecurityToken"),
            new(ClaimTypes.NameIdentifier, "TokenUser"),
            new(ClaimTypes.AuthenticationMethod, "SecurityToken"),
            new("scope", Utils.ApiName)
        };
        var authRoles = Context.Request.Headers["X-AUTH-ROLE"].ToString();
        if (!string.IsNullOrEmpty(authRoles))
        {
            var roles = HttpUtility.UrlDecode(authRoles).Split(',', StringSplitOptions.RemoveEmptyEntries);
            claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role.Trim())));
        }

        Logger.LogDebug("认证码匹配成功 {TraceId} {Identity}", Context.TraceIdentifier,
            JsonSerializer.Serialize(claims.Select(x => new
            {
                type = x.Type,
                value = x.Value
            })));

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        await Task.CompletedTask;
        return AuthenticateResult.Success(ticket);
    }
}