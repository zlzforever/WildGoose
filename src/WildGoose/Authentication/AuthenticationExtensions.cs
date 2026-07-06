using WildGoose.Authentication.GatewayJwtBearer;
using WildGoose.Authentication.JwtBearer;
using WildGoose.Authentication.Token;
using WildGoose.Domain;

namespace WildGoose.Authentication;

public static class AuthenticationExtensions
{
    public static void ConfigAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var apiName = configuration["ApiName"];
        Defaults.ApiName = apiName;
        if (string.IsNullOrEmpty(apiName))
        {
            throw WildGooseFriendlyException.From(ErrorCodes.ApiNameRequired);
        }

        var authenticationSchemeValue = configuration["AuthenticationSchemes"] ??
                                        "GatewayBearer, Bearer, SecurityToken";
        var authenticationSchemes = authenticationSchemeValue.Split(',',
            StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToArray();

        // 验证
        var authenticationBuilder = services.AddAuthentication();
        // JsonHeader Authentication
        if (authenticationSchemes.Contains("GatewayBearer", StringComparer.OrdinalIgnoreCase))
        {
            Defaults.Logger.LogInformation("Adding GatewayJwtBearer authentication");
            services.Configure<GatewayJwtBearerOptions>(configuration.GetSection("GatewayBearer"));
            authenticationBuilder
                .AddScheme<GatewayJwtBearerOptions, GatewayJwtBearerHandler>("GatewayBearer",
                    o =>
                    {
                        // 
                        o.Audience = apiName;
                    });
        }

        // JwtBearer Authentication
        if (authenticationSchemes.Contains("JwtBearer", StringComparer.OrdinalIgnoreCase))
        {
            Defaults.Logger.LogInformation("Adding JwtBearer authentication");
            services.AddJwtBearerAuthentication(authenticationBuilder, configuration, apiName);
        }

        if (authenticationSchemes.Contains("SecurityToken", StringComparer.OrdinalIgnoreCase))
        {
            Defaults.Logger.LogInformation("Adding SecurityTokenJwtBearer authentication");
            authenticationBuilder.AddScheme<TokenAuthOptions, TokenAuthHandler>("SecurityToken",
                tOptions =>
                {
                    tOptions.SecurityToken = Environment.GetEnvironmentVariable("WildGooseSecurityToken") ?? "";
                });
        }

        // 注册授权策略
        services.AddAuthorization(options =>
        {
            options.AddPolicy("SCOPE", policy =>
            {
                policy.AddAuthenticationSchemes(authenticationSchemes);
                policy.RequireAuthenticatedUser();
                policy.RequireClaim("scope", apiName);
            });
            options.AddPolicy(Defaults.SuperOrUserAdminOrOrgAdminPolicy, policy =>
            {
                policy.AddAuthenticationSchemes(authenticationSchemes);
                policy.RequireAuthenticatedUser();
                policy.RequireClaim("scope", apiName);
                policy.RequireRole(Defaults.AdminRole, Defaults.UserAdminRole, Defaults.OrganizationAdminRole);
            });
            options.AddPolicy("USER_ADMIN", policy =>
            {
                policy.AddAuthenticationSchemes(authenticationSchemes);
                policy.RequireAuthenticatedUser();
                policy.RequireClaim("scope", apiName);
                policy.RequireRole(Defaults.UserAdminRole);
            });
            options.AddPolicy("SUPER", policy =>
            {
                policy.AddAuthenticationSchemes(authenticationSchemes);
                policy.RequireAuthenticatedUser();
                policy.RequireClaim("scope", apiName);
                policy.RequireRole(Defaults.AdminRole);
            });
        });
    }
}