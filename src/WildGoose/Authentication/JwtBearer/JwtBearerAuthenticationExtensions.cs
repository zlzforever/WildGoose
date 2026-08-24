using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace WildGoose.Authentication.JwtBearer;

public static class JwtBearerAuthenticationExtensions
{
    public static AuthenticationBuilder AddJwtBearerAuthentication(this IServiceCollection services,
        AuthenticationBuilder builder,
        IConfiguration configuration, string apiName)
    {
        return services.AddJwtBearerAuthentication(
            builder,
            configuration,
            apiName,
            new DefaultHostEnvironment());
    }

    internal static AuthenticationBuilder AddJwtBearerAuthentication(this IServiceCollection services,
        AuthenticationBuilder builder,
        IConfiguration configuration,
        string apiName,
        IHostEnvironment environment)
    {
        var jwtBearerSettings = configuration.GetSection("JwtBearer").Get<JwtBearerSettings>();
        if (jwtBearerSettings == null)
        {
            throw new ArgumentException("JwtBearer options not found in the configuration file.");
        }

        var localKey = LoadLocalKey(jwtBearerSettings, environment);
        var metadataAddress = localKey == null
            ? ResolveAndValidateMetadataAddress(jwtBearerSettings, environment)
            : null;
        var validAudience = string.IsNullOrWhiteSpace(jwtBearerSettings.ValidAudience)
            ? apiName
            : jwtBearerSettings.ValidAudience;

        if (string.IsNullOrWhiteSpace(validAudience))
        {
            throw new InvalidOperationException("JwtBearer:ValidAudience and ApiName cannot both be empty.");
        }

        if (localKey != null && jwtBearerSettings.ValidateIssuer &&
            string.IsNullOrWhiteSpace(jwtBearerSettings.ValidIssuer))
        {
            throw new InvalidOperationException(
                "JwtBearer:ValidIssuer is required when JwtBearer:KeyPath selects local JWK validation.");
        }

        if (!environment.IsDevelopment() &&
            (!jwtBearerSettings.ValidateIssuer ||
             !jwtBearerSettings.ValidateAudience ||
             !jwtBearerSettings.ValidateLifetime))
        {
            throw new InvalidOperationException(
                "Production JwtBearer configuration must enable issuer, audience, and lifetime validation.");
        }

        if (localKey != null)
        {
            services.AddKeyedSingleton("JwtBearerRsaSecurityKey", localKey);
        }

        builder.AddJwtBearer("JwtBearer", options =>
        {
            if (localKey != null)
            {
                options.Authority = null;
                options.MetadataAddress = null!;
                options.ConfigurationManager = null;
            }
            else
            {
                options.Authority = jwtBearerSettings.Authority?.TrimEnd('/');
                options.MetadataAddress = metadataAddress!;
                options.RequireHttpsMetadata = jwtBearerSettings.RequireHttpsMetadata;
            }

            options.Audience = validAudience;
            options.MapInboundClaims = false;
            options.SaveToken = false;
            options.IncludeErrorDetails = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = localKey,
                ValidateIssuer = jwtBearerSettings.ValidateIssuer,
                ValidIssuer = jwtBearerSettings.ValidIssuer,
                ValidateAudience = jwtBearerSettings.ValidateAudience,
                ValidAudience = validAudience,
                ValidateLifetime = jwtBearerSettings.ValidateLifetime,
                RequireSignedTokens = true,
                NameClaimType = ClaimTypes.Name,
                RoleClaimType = ClaimTypes.Role,
                IssuerValidator = string.IsNullOrWhiteSpace(jwtBearerSettings.ValidIssuer)
                    ? null
                    : CreateExactIssuerValidator(jwtBearerSettings.ValidIssuer)
            };

            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = ctx =>
                {
                    NormalizeClaims(ctx.Principal);
                    return Task.CompletedTask;
                }
            };
        });

        return builder;
    }

    private static RsaSecurityKey? LoadLocalKey(JwtBearerSettings settings, IHostEnvironment environment)
    {
        if (string.IsNullOrWhiteSpace(settings.KeyPath))
        {
            return null;
        }

        var path = settings.ResolveKeyPath(environment);
        var key = RsaSecurityKeyHelper.GetRsaSecurityKey(path);
        if (key == null)
        {
            throw new InvalidOperationException(
                $"Unable to load RSA JWK from JwtBearer:KeyPath '{path}'. The application will not fall back to OIDC metadata.");
        }

        return key;
    }

    private static string ResolveAndValidateMetadataAddress(
        JwtBearerSettings settings,
        IHostEnvironment environment)
    {
        if (string.IsNullOrWhiteSpace(settings.MetadataAddress) &&
            string.IsNullOrWhiteSpace(settings.Authority))
        {
            throw new InvalidOperationException(
                "JwtBearer requires either JwtBearer:Authority or JwtBearer:MetadataAddress when JwtBearer:KeyPath is empty.");
        }

        var metadataAddress = string.IsNullOrWhiteSpace(settings.MetadataAddress)
            ? BuildMetadataAddress(settings.Authority)
            : ParseAbsoluteHttpUri(settings.MetadataAddress, "JwtBearer:MetadataAddress");

        if (!string.Equals(metadataAddress.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(metadataAddress.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "JwtBearer metadata address must use the http or https scheme.");
        }

        if (metadataAddress.Scheme == Uri.UriSchemeHttp && settings.RequireHttpsMetadata)
        {
            throw new InvalidOperationException(
                "JwtBearer:RequireHttpsMetadata=true is incompatible with an http metadata address.");
        }

        if (environment.IsDevelopment() &&
            metadataAddress.Scheme == Uri.UriSchemeHttps &&
            !settings.RequireHttpsMetadata)
        {
            throw new InvalidOperationException(
                "Development JwtBearer metadata may disable RequireHttpsMetadata only for an explicit http endpoint.");
        }

        if (!environment.IsDevelopment() &&
            (metadataAddress.Scheme != Uri.UriSchemeHttps || !settings.RequireHttpsMetadata))
        {
            throw new InvalidOperationException(
                "Production JwtBearer metadata must use https and JwtBearer:RequireHttpsMetadata=true.");
        }

        return metadataAddress.AbsoluteUri;
    }

    private static Uri BuildMetadataAddress(string? authority)
    {
        var authorityUri = ParseAbsoluteHttpUri(authority, "JwtBearer:Authority");
        var builder = new UriBuilder(authorityUri)
        {
            Query = string.Empty,
            Fragment = string.Empty,
            Path = $"{authorityUri.AbsolutePath.TrimEnd('/')}/.well-known/openid-configuration"
        };
        return builder.Uri;
    }

    private static Uri ParseAbsoluteHttpUri(string? value, string configurationKey)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) ||
            string.IsNullOrWhiteSpace(uri.Host))
        {
            throw new InvalidOperationException(
                $"{configurationKey} must be an absolute http(s) URL.");
        }

        return uri;
    }

    private static IssuerValidator CreateExactIssuerValidator(string expectedIssuer)
    {
        return (issuer, _, _) =>
        {
            if (!string.Equals(issuer, expectedIssuer, StringComparison.Ordinal))
            {
                throw new SecurityTokenInvalidIssuerException(
                    "The JwtBearer token issuer does not match the configured issuer.");
            }

            return issuer!;
        };
    }

    private static void NormalizeClaims(ClaimsPrincipal? principal)
    {
        if (principal == null)
        {
            return;
        }

        foreach (var identity in principal.Identities)
        {
            NormalizeScopeClaims(identity);
            NormalizeRoleClaims(identity);
            NormalizeNameClaims(identity);
        }
    }

    private static void NormalizeScopeClaims(ClaimsIdentity identity)
    {
        var claims = identity.FindAll("scope").ToList();
        foreach (var claim in claims)
        {
            identity.RemoveClaim(claim);
        }

        foreach (var value in claims.SelectMany(claim =>
                     claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)))
        {
            identity.AddClaim(new Claim("scope", value));
        }
    }

    private static void NormalizeRoleClaims(ClaimsIdentity identity)
    {
        var claims = identity.FindAll("role").ToList();
        foreach (var claim in claims)
        {
            identity.RemoveClaim(claim);
            identity.AddClaim(new Claim(ClaimTypes.Role, claim.Value));
        }
    }

    private static void NormalizeNameClaims(ClaimsIdentity identity)
    {
        var claims = identity.FindAll("name").ToList();
        foreach (var claim in claims)
        {
            identity.RemoveClaim(claim);
            identity.AddClaim(new Claim(ClaimTypes.Name, claim.Value));
        }
    }

    private sealed class DefaultHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } =
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ??
            Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ??
            Environments.Production;

        public string ApplicationName { get; set; } = typeof(Program).Assembly.GetName().Name!;
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
