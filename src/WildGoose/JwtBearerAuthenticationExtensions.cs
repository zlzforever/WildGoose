using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using WildGoose.Application;
using WildGoose.Domain;
using WildGoose.Filters;

namespace WildGoose;

public static class JwtBearerAuthenticationExtensions
{
    public static void AddJwtBearerAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtBearerOptions = configuration.GetSection("JwtBearer").Get<JwtBearerSettings>();
        if (jwtBearerOptions == null)
        {
            throw new ArgumentException("JwtBearerOptions is null.");
        }

        RsaSecurityKey? rsaSecurityKey = null;
        if (jwtBearerOptions.Key != null)
        {
            rsaSecurityKey = jwtBearerOptions.Key.GetRsaSecurityKey();
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
                        "JwtBearer:Authority is null or empty.");
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
            options.AddPolicy("JWT", policy =>
            {
                policy.RequireAssertion(context =>
                {
                    // 检查是否已有成功的认证
                    return context.User.Identities.Any(i => i.IsAuthenticated);
                });
                policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
                policy.RequireAuthenticatedUser();
                policy.RequireClaim("scope", apiName);
            });
        });
    }
}