using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using WildGoose.Application;

namespace WildGoose.Filters;

public class TokenAuthHandler : AuthenticationHandler<TokenAuthOptions>
{
    public TokenAuthHandler(IOptionsMonitor<TokenAuthOptions> options, ILoggerFactory logger, UrlEncoder encoder,
        ISystemClock clock) : base(options, logger, encoder, clock)
    {
    }

    public TokenAuthHandler(IOptionsMonitor<TokenAuthOptions> options, ILoggerFactory logger, UrlEncoder encoder) :
        base(options, logger, encoder)
    {
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var actionContext = Context.GetEndpoint();
        if (actionContext == null)
        {
            return AuthenticateResult.Fail("No endpoint found");
        }

        var token = Context.Request.Headers["X-WD-TOKEN"].ToString();

        if (string.IsNullOrEmpty(token))
        {
            Logger.LogError("授权码不存在 {TraceId}", Context.TraceIdentifier);
            return AuthenticateResult.Fail("401");
        }

        if (token != Options.Token)
        {
            Logger.LogError("授权码不正确 {TraceId}", Context.TraceIdentifier);
            return AuthenticateResult.Fail("401");
        }

        Logger.LogDebug("验签成功 traceId {TraceId}", Context.TraceIdentifier);

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "TokenUser"),
            new(ClaimTypes.NameIdentifier, "TokenUser"),
            new(ClaimTypes.AuthenticationMethod, "Token"),
            new("scope", Utils.ApiName)
        };
        var roleStr = Context.Request.Headers["X-WD-ROLE"].ToString();
        if (!string.IsNullOrEmpty(roleStr))
        {
            var roles = roleStr.Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role.Trim()));
            }
        }

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        await Task.CompletedTask;
        return AuthenticateResult.Success(ticket);
    }
}