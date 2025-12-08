using System.Security.Cryptography;
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
                    ValidateAudience =
                        "true".Equals(configuration["JwtBearer:ValidateAudience"],
                            StringComparison.OrdinalIgnoreCase),
                    ValidateIssuer = "true".Equals(configuration["JwtBearer:ValidateIssuer"],
                        StringComparison.OrdinalIgnoreCase),
                    ValidIssuer = configuration["JwtBearer:ValidIssuer"],
                    ValidAudience = configuration["JwtBearer:ValidAudience"],
                    ValidateLifetime = "true".Equals(configuration["JwtBearer:ValidateLifetime"],
                        StringComparison.OrdinalIgnoreCase)
                };

                var keyType = configuration["JwtBearer:Key:kty"];
                if (!string.IsNullOrEmpty(keyType))
                {
                    var webKey = configuration.GetSection("JwtBearer:Key").Get<JsonWebKey>();
                    if (webKey == null)
                    {
                        throw new ArgumentException("JwtBearer:Key is null");
                    }

                    var rsaParameters = new RSAParameters
                    {
                        Modulus = Base64UrlEncoder.DecodeBytes(webKey.N),
                        Exponent = Base64UrlEncoder.DecodeBytes(webKey.E),
                        D = string.IsNullOrEmpty(webKey.D) ? null : Base64UrlEncoder.DecodeBytes(webKey.D),
                        P = string.IsNullOrEmpty(webKey.P) ? null : Base64UrlEncoder.DecodeBytes(webKey.P),
                        Q = string.IsNullOrEmpty(webKey.Q) ? null : Base64UrlEncoder.DecodeBytes(webKey.Q),
                        DP = string.IsNullOrEmpty(webKey.DP) ? null : Base64UrlEncoder.DecodeBytes(webKey.DP),
                        DQ = string.IsNullOrEmpty(webKey.DQ) ? null : Base64UrlEncoder.DecodeBytes(webKey.DQ),
                        InverseQ = string.IsNullOrEmpty(webKey.QI) ? null : Base64UrlEncoder.DecodeBytes(webKey.QI)
                    };

                    // 可选：禁用自动发现配置的额外校验
                    options.ConfigurationManager = null;
                    options.Authority = null;
                    options.RequireHttpsMetadata = false;
                    options.TokenValidationParameters.IssuerSigningKey = new RsaSecurityKey(rsaParameters);
                }
                else
                {
                    options.Authority = configuration["JwtBearer:Authority"] ?? throw new ApplicationException(
                        "JwtBearer:Authority is null or empty.");

                    options.RequireHttpsMetadata = "true".Equals(configuration["JwtBearer:RequireHttpsMetadata"],
                        StringComparison.OrdinalIgnoreCase);
                    options.MetadataAddress = configuration["JwtBearer:MetadataAddress"] ?? string.Empty;

                    // 试验性代码，authority 不设计 https/requireHttpsMetadata
                    if (!options.RequireHttpsMetadata && string.IsNullOrEmpty(options.MetadataAddress) &&
                        !string.IsNullOrEmpty(options.Authority))
                    {
                        var metadataAddress =
                            options.Authority.Replace("https://", "http://", StringComparison.OrdinalIgnoreCase);
                        if (!metadataAddress.EndsWith("/", StringComparison.Ordinal))
                        {
                            metadataAddress += "/";
                        }

                        options.MetadataAddress = metadataAddress + ".well-known/openid-configuration";
                    }
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
                policy.AddAuthenticationSchemes("Bearer");
                policy.RequireAuthenticatedUser();
                policy.RequireClaim("scope", apiName);
            });
        });
    }
}