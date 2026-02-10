using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using WildGoose.Application;
using WildGoose.Domain;
using WildGoose.Filters;

namespace WildGoose;

public static class AuthenticationExtensions
{
    public static void ConfigAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtBearerOptions = configuration.GetSection("JwtBearer").Get<JwtBearerSettings>();
        if (jwtBearerOptions == null)
        {
            return;
        }

        var rsaSecurityKey = jwtBearerOptions.GetRsaSecurityKey();
        if (rsaSecurityKey != null)
        {
            services.AddKeyedSingleton(JwtBearerSettings.JwtBearerRsaSecurityKey, rsaSecurityKey);
        }

        var authenticationBuilder = services.AddAuthentication(x =>
            {
                x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
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
                        "JwtBearer:Authority is null or empty. Please check your configuration. https://qcn6sgdfwyfj.feishu.cn/wiki/O4QEwz6idiwHFsk8V3EcLE7Unpf?fromScene=spaceOverview#share-VPlFdJAwSo2Oyyxs7XPcWHy4nQd");
                    options.RequireHttpsMetadata = jwtBearerOptions.RequireHttpsMetadata;
                    options.MetadataAddress = jwtBearerOptions.GetMetadataAddress();
                }
            });
        authenticationBuilder.AddScheme<TokenAuthOptions, TokenAuthHandler>("SecurityToken",
            tOptions =>
            {
                tOptions.SecurityToken = Environment.GetEnvironmentVariable("WildGooseSecurityToken") ?? "";
            });

        // adds an authorization policy to make sure the token is for scope 'api1'
        var apiName = configuration["ApiName"];
        Utils.ApiName = apiName;
        if (string.IsNullOrEmpty(apiName))
        {
            throw new WildGooseFriendlyException(1, "ApiName is null or empty");
        }

        services.AddAuthorization(options =>
        {
            options.AddPolicy(Defaults.SuperOrUserAdminOrOrgAdminPolicy, policy =>
            {
                policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme, "SecurityToken");
                policy.RequireAuthenticatedUser();
                policy.RequireClaim("scope", apiName);
                policy.RequireRole("admin", "user-admin", "organization-admin");
            });
            options.AddPolicy("USER_ADMIN", policy =>
            {
                policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme, "SecurityToken");
                policy.RequireAuthenticatedUser();
                policy.RequireClaim("scope", apiName);
                policy.RequireRole("user-admin");
            });
            options.AddPolicy("SUPER", policy =>
            {
                policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme, "SecurityToken");
                policy.RequireAuthenticatedUser();
                policy.RequireClaim("scope", apiName);
                policy.RequireRole("admin");
            });
        });
    }
}