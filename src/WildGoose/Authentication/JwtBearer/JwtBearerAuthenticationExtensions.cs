using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using WildGoose.Domain;

namespace WildGoose.Authentication.JwtBearer;

public static class JwtBearerAuthenticationExtensions
{
    public static AuthenticationBuilder AddJwtBearerAuthentication(this IServiceCollection services,
        AuthenticationBuilder builder,
        IConfiguration configuration, string apiName)
    {
        var jwtBearerOptions = configuration.GetSection("JwtBearer").Get<JwtBearerSettings>();
        if (jwtBearerOptions == null)
        {
            throw new ArgumentException("JwtBearer options not found in the configuration file.");
        }

        var rsaSecurityKey = RsaSecurityKeyHelper.GetRsaSecurityKey(jwtBearerOptions.KeyPath);
        if (rsaSecurityKey != null)
        {
            services.AddKeyedSingleton("JwtBearerRsaSecurityKey", rsaSecurityKey);
        }

        builder.AddJwtBearer("JwtBearer", options =>
        {
            // 1. 不要设置 Authority！
            options.Authority = null;

            options.Audience = apiName;
            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    context.Response.StatusCode = 401;
                    return Task.CompletedTask;
                }
            };

            // Create TokenValidationParameters FIRST so the if/else below
            // sets keys/configuration on it without being overwritten
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

            // 2. 手动设置元数据地址（或直接给密钥）
            if (rsaSecurityKey != null)
            {
                options.TokenValidationParameters.IssuerSigningKey = rsaSecurityKey;
                options.ConfigurationManager = null;
            }
            else
            {
                options.MetadataAddress = jwtBearerOptions.GetMetadataAddress();
                options.ConfigurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                    jwtBearerOptions.GetMetadataAddress(),
                    new OpenIdConnectConfigurationRetriever(),
                    new HttpDocumentRetriever(new HttpClient(new HttpClientProxy()))
                );
            }

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

    private class JwtBearerSettings
    {
        public string? Authority { get; set; }
        public bool RequireHttpsMetadata { get; set; } = true;
        public bool ValidateAudience { get; set; } = true;
        public bool ValidateIssuer { get; set; } = true;
        public string? ValidIssuer { get; set; }
        public bool ValidateLifetime { get; set; } = true;

        // ReSharper disable once UnusedAutoPropertyAccessor.Global
        public string? MetadataAddress { get; set; }
        public string? KeyPath { get; set; }

        /// <summary>
        /// Builds the OIDC metadata address from <see cref="Authority"/> preserving the original scheme.
        /// Falls back to <see cref="RequireHttpsMetadata"/> only when Authority has no explicit scheme.
        /// </summary>
        public string GetMetadataAddress()
        {
            if (!string.IsNullOrEmpty(MetadataAddress))
            {
                return MetadataAddress;
            }

            if (!string.IsNullOrEmpty(Authority))
            {
                var authority = Authority.TrimEnd('/');
                string scheme;
                if (authority.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    scheme = "https";
                    authority = authority["https://".Length..];
                }
                else if (authority.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                {
                    scheme = "http";
                    authority = authority["http://".Length..];
                }
                else
                {
                    scheme = RequireHttpsMetadata ? "https" : "http";
                }
                return $"{scheme}://{authority}/.well-known/openid-configuration";
            }

            throw new ArgumentException("Authority or MetadataAddress cannot be null or empty.");
        }
    }

    class HttpClientProxy : HttpClientHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                Defaults.Logger.LogWarning(
                    "OIDC Discovery request to {RequestUri} returned {StatusCode}: {ResponseBody}",
                    request.RequestUri, (int)response.StatusCode, body);
            }

            return response;
        }
    }
}
