using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using WildGoose.Authentication.GatewayJwtBearer;
using WildGoose.Authentication.JwtBearer;
using WildGoose.Authentication.Token;
using WildGoose.Domain;

namespace WildGoose.Authentication;

public static class AuthenticationExtensions
{
    public static void ConfigAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        ConfigAuthenticationCore(services, configuration, new DefaultHostEnvironment());
    }

    internal static void ConfigAuthenticationCore(this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var apiName = configuration["ApiName"];
        Defaults.ApiName = apiName;
        if (string.IsNullOrWhiteSpace(apiName))
        {
            throw WildGooseFriendlyException.From(ErrorCodes.ApiNameRequired);
        }

        var authenticationSchemes = ParseAuthenticationSchemes(configuration["AuthenticationSchemes"]);
        var defaultScheme = authenticationSchemes.Contains("JwtBearer", StringComparer.OrdinalIgnoreCase)
            ? "JwtBearer"
            : authenticationSchemes[0];
        var authenticationBuilder = services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = defaultScheme;
            options.DefaultChallengeScheme = defaultScheme;
            options.DefaultForbidScheme = defaultScheme;
        });

        if (authenticationSchemes.Contains("GatewayBearer", StringComparer.OrdinalIgnoreCase))
        {
            Defaults.Logger.LogInformation("Adding GatewayJwtBearer authentication");
            var gatewaySection = configuration.GetSection("GatewayBearer");
            if (!gatewaySection.GetChildren().Any())
            {
                gatewaySection = configuration.GetSection("GatewayJwtBearer");
            }

            services.Configure<GatewayJwtBearerOptions>(gatewaySection);
            services.Configure<GatewayJwtBearerOptions>("GatewayBearer", gatewaySection);
            authenticationBuilder
                .AddScheme<GatewayJwtBearerOptions, GatewayJwtBearerHandler>("GatewayBearer",
                    options =>
                    {
                        if (string.IsNullOrWhiteSpace(options.Audience))
                        {
                            options.Audience = apiName;
                        }
                    });
        }

        if (authenticationSchemes.Contains("JwtBearer", StringComparer.OrdinalIgnoreCase))
        {
            Defaults.Logger.LogInformation("Adding JwtBearer authentication");
            services.AddJwtBearerAuthentication(authenticationBuilder, configuration, apiName, environment);
        }

        if (authenticationSchemes.Contains("SecurityToken", StringComparer.OrdinalIgnoreCase))
        {
            Defaults.Logger.LogInformation("Adding SecurityTokenJwtBearer authentication");
            authenticationBuilder.AddScheme<TokenAuthOptions, TokenAuthHandler>("SecurityToken",
                options =>
                {
                    options.SecurityToken = Environment.GetEnvironmentVariable("WildGooseSecurityToken") ?? "";
                });
        }

        services.AddAuthorization(options =>
        {
            options.DefaultPolicy = new AuthorizationPolicyBuilder(authenticationSchemes)
                .RequireAuthenticatedUser()
                .Build();
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

    private static string[] ParseAuthenticationSchemes(string? configuredSchemes)
    {
        var rawSchemes = string.IsNullOrWhiteSpace(configuredSchemes)
            ? ["JwtBearer"]
            : configuredSchemes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var authenticationSchemes = rawSchemes
            .Select(NormalizeScheme)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (authenticationSchemes.Length == 0)
        {
            throw new ArgumentException("AuthenticationSchemes must contain at least one authentication scheme.");
        }

        var supportedSchemes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "GatewayBearer",
            "JwtBearer",
            "SecurityToken"
        };
        var unknownScheme = authenticationSchemes.FirstOrDefault(x => !supportedSchemes.Contains(x));
        if (unknownScheme != null)
        {
            throw new ArgumentException(
                $"AuthenticationSchemes contains unknown scheme '{unknownScheme}'. Registered schemes are GatewayBearer, JwtBearer, and SecurityToken.");
        }

        return authenticationSchemes;
    }

    private static string NormalizeScheme(string scheme)
    {
        if (string.Equals(scheme, "Bearer", StringComparison.OrdinalIgnoreCase))
        {
            return "JwtBearer";
        }

        if (string.Equals(scheme, "GatewayJwtBearer", StringComparison.OrdinalIgnoreCase))
        {
            return "GatewayBearer";
        }

        if (string.Equals(scheme, "GatewayBearer", StringComparison.OrdinalIgnoreCase))
        {
            return "GatewayBearer";
        }

        if (string.Equals(scheme, "JwtBearer", StringComparison.OrdinalIgnoreCase))
        {
            return "JwtBearer";
        }

        if (string.Equals(scheme, "SecurityToken", StringComparison.OrdinalIgnoreCase))
        {
            return "SecurityToken";
        }

        return scheme;
    }

    private sealed class DefaultHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } =
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ??
            Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ??
            Environments.Production;

        public string ApplicationName { get; set; } = typeof(Program).Assembly.GetName().Name!;
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
