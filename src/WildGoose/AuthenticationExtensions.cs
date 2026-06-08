using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using WildGoose.Authentication.GatewayJwtBearer;
using WildGoose.Authentication.JwtBearer;
using WildGoose.Authentication.Token;
using WildGoose.Domain;

namespace WildGoose;

public static class AuthenticationExtensions
{
    public static void ConfigAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        // 验证
        var authenticationBuilder = services.AddAuthentication();
        // JsonHeader Authentication
        services.Configure<GatewayJwtBearerOptions>(configuration.GetSection("GatewayBearer"));
        authenticationBuilder
            .AddScheme<GatewayJwtBearerOptions, GatewayJwtBearerHandler>("GatewayBearer", _ => { });

        // JwtBearer Authentication
        services.Configure<JwtBearerSettings>(configuration.GetSection("JwtBearer"));
        services.AddJwtBearerAuthentication(authenticationBuilder, configuration);

        authenticationBuilder.AddScheme<TokenAuthOptions, TokenAuthHandler>("SecurityToken",
            tOptions =>
            {
                tOptions.SecurityToken = Environment.GetEnvironmentVariable("WildGooseSecurityToken") ?? "";
            });

        // adds an authorization policy to make sure the token is for scope 'api1'
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
        IConfiguration configuration)
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
            .AddJwtBearer(options =>
            {
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
                    ValidateAudience = jwtBearerOptions.ValidateAudience,
                    ValidateIssuer = jwtBearerOptions.ValidateIssuer,
                    ValidIssuer = jwtBearerOptions.ValidIssuer,
                    ValidAudience = jwtBearerOptions.ValidAudience,
                    ValidateLifetime = jwtBearerOptions.ValidateLifetime
                };

                if (rsaSecurityKey != null)
                {
                    // 可选：禁用自动发现配置的额外校验
                    options.ConfigurationManager = null;
                    options.Authority = null;
                    options.RequireHttpsMetadata = false;
                    options.TokenValidationParameters.IssuerSigningKey = rsaSecurityKey;
                }
                else
                {
                    options.Authority = jwtBearerOptions.Authority ?? throw new ApplicationException(
                        "JwtBearer:Authority is null or empty. Please check your configuration.");
                    options.RequireHttpsMetadata = jwtBearerOptions.RequireHttpsMetadata;
                    options.MetadataAddress = jwtBearerOptions.GetMetadataAddress();
                }
            });

        return builder;
    }
}