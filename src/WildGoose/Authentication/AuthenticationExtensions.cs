using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;
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
            Log.Logger.Information("Adding GatewayJwtBearer authentication");
            services.Configure<GatewayJwtBearerOptions>(configuration.GetSection("GatewayBearer"));
            authenticationBuilder
                .AddScheme<GatewayJwtBearerOptions, GatewayJwtBearerHandler>("GatewayBearer",
                    o => { o.Audience = apiName; });
        }

        // JwtBearer Authentication
        if (authenticationSchemes.Contains("JwtBearer", StringComparer.OrdinalIgnoreCase))
        {
            Log.Logger.Information("Adding JwtBearer authentication");
            services.Configure<JwtBearerSettings>(configuration.GetSection("JwtBearer"));
            services.AddJwtBearerAuthentication(authenticationBuilder, configuration, apiName);
        }

        if (authenticationSchemes.Contains("SecurityToken", StringComparer.OrdinalIgnoreCase))
        {
            Log.Logger.Information("Adding SecurityTokenJwtBearer authentication");
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
                policy.RequireAuthenticatedUser().RequireClaim("scope", apiName);
            });
        });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(Defaults.SuperOrUserAdminOrOrgAdminPolicy, policy =>
            {
                policy.AddAuthenticationSchemes(authenticationSchemes);
                policy.RequireAuthenticatedUser();
                policy.RequireClaim("scope", apiName);
                policy.RequireRole(Defaults.Admin, Defaults.UserAdmin, Defaults.OrganizationAdmin);
            });
            options.AddPolicy("USER_ADMIN", policy =>
            {
                policy.AddAuthenticationSchemes(authenticationSchemes);
                policy.RequireAuthenticatedUser();
                policy.RequireClaim("scope", apiName);
                policy.RequireRole(Defaults.UserAdmin);
            });
            options.AddPolicy("SUPER", policy =>
            {
                policy.AddAuthenticationSchemes(authenticationSchemes);
                policy.RequireAuthenticatedUser();
                policy.RequireClaim("scope", apiName);
                policy.RequireRole(Defaults.Admin);
            });
        });
    }

    public static AuthenticationBuilder AddJwtBearerAuthentication(this IServiceCollection services,
        AuthenticationBuilder builder,
        IConfiguration configuration, string apiName)
    {
        var jwtBearerOptions = configuration.GetSection("JwtBearer").Get<JwtBearerSettings>();
        if (jwtBearerOptions == null)
        {
            return builder;
        }

        var rsaSecurityKey = jwtBearerOptions.GetRsaSecurityKey();
        if (rsaSecurityKey != null)
        {
            services.AddKeyedSingleton(JwtBearerSettings.JwtBearerRsaSecurityKey, rsaSecurityKey);
        }

        builder
            .AddJwtBearer("JwtBearer", options =>
            {
                // 1. 不要设置 Authority！
                options.Authority = null;

                // 2. 手动设置元数据地址（或直接给密钥）
                if (rsaSecurityKey != null)
                {
                    // 可选：禁用自动发现配置的额外校验
                    options.ConfigurationManager = null;
                    options.TokenValidationParameters.IssuerSigningKey = rsaSecurityKey;
                }
                else
                {
                    // 手动设置元数据地址
                    options.MetadataAddress = jwtBearerOptions.GetMetadataAddress();
                }

                options.Audience = apiName;
                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        context.Response.StatusCode = 401;
                        return Task.CompletedTask;
                    }
                };
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    // Aud 验证
                    ValidateAudience = jwtBearerOptions.ValidateAudience,
                    // 开启 Issuer 验证
                    ValidateIssuer = jwtBearerOptions.ValidateIssuer,
                    ValidIssuer = jwtBearerOptions.ValidIssuer,
                    // 验证过期
                    ValidateLifetime = jwtBearerOptions.ValidateLifetime
                };
                // 关键2：Token解析完成后，拦截拆分scope为多条Claim
                options.Events.OnTokenValidated = ctx =>
                {
                    if (ctx.Principal == null)
                    {
                        return Task.CompletedTask;
                    }

                    var scopeClaim = ctx.Principal.FindFirst("scope");
                    if (scopeClaim != null && !string.IsNullOrWhiteSpace(scopeClaim.Value))
                    {
                        var scopes = scopeClaim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (ctx.Principal.Identity is not ClaimsIdentity identity)
                        {
                            return Task.CompletedTask;
                        }

                        // 删除原始单条scope，插入多条独立scope claim
                        identity.RemoveClaim(scopeClaim);
                        foreach (var s in scopes)
                        {
                            identity.AddClaim(new Claim("scope", s));
                        }
                    }

                    return Task.CompletedTask;
                };
            });

        return builder;
    }
}