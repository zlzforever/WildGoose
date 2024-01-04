using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using WildGoose.Domain;

namespace WildGoose;

public static class IdentityServerExtensions
{
    public static void ConfigureIdentityServer(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthentication(x =>
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
                options.Authority = configuration["Authority"];
                options.RequireHttpsMetadata = "true".Equals(configuration["RequireHttpsMetadata"],
                    StringComparison.OrdinalIgnoreCase);
                options.TokenValidationParameters = new TokenValidationParameters { ValidateAudience = false };
            });

        // adds an authorization policy to make sure the token is for scope 'api1'
        var apiName = configuration["ApiName"];
        if (string.IsNullOrEmpty(apiName))
        {
            throw new WildGooseFriendlyException(1, "ApiName is null or empty");
        }

        services.AddAuthorization(options =>
        {
            options.AddPolicy("Jwt", policy =>
            {
                policy.AddAuthenticationSchemes("Bearer");
                policy.RequireAuthenticatedUser();
                policy.RequireClaim("scope", apiName);
            });
        });
    }
}