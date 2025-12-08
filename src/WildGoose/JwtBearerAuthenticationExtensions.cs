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
        var authority = configuration["JwtBearer:Authority"];
        if (string.IsNullOrEmpty(authority))
        {
            throw new ApplicationException(
                "JwtBearer:Authority is null or empty. Please check your configuration. https://qcn6sgdfwyfj.feishu.cn/wiki/O4QEwz6idiwHFsk8V3EcLE7Unpf?fromScene=spaceOverview#share-VPlFdJAwSo2Oyyxs7XPcWHy4nQd");
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

                options.Authority = authority;
                options.RequireHttpsMetadata = "true".Equals(configuration["JwtBearer:RequireHttpsMetadata"],
                    StringComparison.OrdinalIgnoreCase);
                options.MetadataAddress = configuration["JwtBearer:MetadataAddress"] ?? string.Empty;
                // 试验性代码，authority 不设计 https/requireHttpsMetadata
                if (!options.RequireHttpsMetadata && string.IsNullOrEmpty(options.MetadataAddress))
                {
                    var metadataAddress =
                        options.Authority.Replace("https://", "http://", StringComparison.OrdinalIgnoreCase);
                    if (!metadataAddress.EndsWith("/", StringComparison.Ordinal))
                    {
                        metadataAddress += "/";
                    }

                    options.MetadataAddress = metadataAddress + ".well-known/openid-configuration";
                }

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateAudience =
                        "true".Equals(configuration["JwtBearer:ValidateAudience"], StringComparison.OrdinalIgnoreCase),
                    ValidateIssuer = "true".Equals(configuration["JwtBearer:ValidateIssuer"],
                        StringComparison.OrdinalIgnoreCase),
                    ValidIssuer = configuration["JwtBearer:ValidIssuer"],
                    ValidAudience = configuration["JwtBearer:ValidAudience"]
                };
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
                policy.AddAuthenticationSchemes("Bearer");
                policy.RequireAuthenticatedUser();
                policy.RequireClaim("scope", apiName);
            });
        });
    }
}