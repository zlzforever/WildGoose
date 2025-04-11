using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using WildGoose.Application;
using WildGoose.Domain;
using WildGoose.Filters;

namespace WildGoose;

public static class IdentityServerExtensions
{
    public static void ConfigureIdentityServer(this IServiceCollection services, IConfiguration configuration)
    {
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
                options.Authority = configuration["JwtBearer:Authority"];
                options.RequireHttpsMetadata = "true".Equals(configuration["JwtBearer:RequireHttpsMetadata"],
                    StringComparison.OrdinalIgnoreCase);
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateAudience =
                        "true".Equals(configuration["JwtBearer:ValidateAudience"], StringComparison.OrdinalIgnoreCase),
                    ValidateIssuer = "true".Equals(configuration["JwtBearer:ValidateIssuer"],
                        StringComparison.OrdinalIgnoreCase),
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