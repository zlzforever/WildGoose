using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using WildGoose.Domain;

namespace WildGoose.Authentication.JwtBearer;

public static class JwtBearerAuthenticationExtensions
{
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

        var validAudience = string.IsNullOrWhiteSpace(jwtBearerSettings.ValidAudience)
            ? apiName
            : jwtBearerSettings.ValidAudience;

        if (string.IsNullOrWhiteSpace(validAudience))
        {
            throw new InvalidOperationException("JwtBearer:ValidAudience and ApiName cannot both be empty.");
        }

        if (jwtBearerSettings.ValidateIssuer &&
            string.IsNullOrWhiteSpace(jwtBearerSettings.ValidIssuer))
        {
            throw new InvalidOperationException(
                "JwtBearer:ValidIssuer is required when JwtBearer:ValidateIssuer is true.");
        }

        // 生产环境 issuer/audience/lifetime 必填
        if (!environment.IsDevelopment() &&
            (!jwtBearerSettings.ValidateIssuer ||
             !jwtBearerSettings.ValidateAudience ||
             !jwtBearerSettings.ValidateLifetime))
        {
            throw new InvalidOperationException(
                "Production JwtBearer configuration must enable issuer, audience, and lifetime validation.");
        }

        var localKey = LoadLocalKey(jwtBearerSettings);
        if (localKey != null)
        {
            // 用于自己构造 Claims
            services.AddKeyedSingleton("JwtBearerRsaSecurityKey", localKey);
        }

        builder.AddJwtBearer("JwtBearer", options =>
        {
            // 自建 JWT（无 OIDC IdP，静态密钥）
            // 不要设置 Authority，MetadataAddress，RequireHttpsMetadata
            if (localKey != null)
            {
                options.Authority = null;
                options.MetadataAddress = null!;
                options.ConfigurationManager = null;
            }
            else
            {
                // 冲突：如果你手动设置了 `ConfigurationManager`，Authority **失效**。
                options.Authority = jwtBearerSettings.Authority?.TrimEnd('/');
                // 仅设置 MetadataAddress，**不设置 Authority → 不会自动填充 ValidIssuer，iss 校验失败**。
                if (jwtBearerSettings.MetadataAddress != null)
                {
                    options.MetadataAddress = jwtBearerSettings.MetadataAddress;
                }

                // 是否强制**发现文档 (metadata) 端点必须为 HTTPS**
                // 仅管控 `.well-known/openid-configuration` 的下载地址协议；**不会管控 jwks_uri**
                options.RequireHttpsMetadata = jwtBearerSettings.RequireHttpsMetadata;
            }

            options.Audience = validAudience;
            // 控制 **JWT payload 字段 → .NET Claim 类型名称 的自动映射转换开关**
            // sub -> ClaimTypes.NameIdentifier: http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier
            // 关闭是现在 WebApi / OIDC 项目**最推荐的现代配置**。
            options.MapInboundClaims = false;
            // 关闭后 HttpContext.GetTokenAsync("access_token") 返回 null，但可节省内存
            options.SaveToken = false;
            options.IncludeErrorDetails = !environment.IsProduction();
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = jwtBearerSettings.ValidateIssuerSigningKey,
                IssuerSigningKey = localKey,
                ValidateIssuer = jwtBearerSettings.ValidateIssuer,
                ValidIssuer = jwtBearerSettings.ValidIssuer,
                ValidateAudience = jwtBearerSettings.ValidateAudience,
                ValidAudience = validAudience,
                ValidateLifetime = jwtBearerSettings.ValidateLifetime,
                RequireSignedTokens = true,
                NameClaimType = JwtClaimTypes.Subject,
                RoleClaimType = JwtClaimTypes.Role
            };
            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    context.Response.StatusCode = 401;
                    return Task.CompletedTask;
                },
                OnTokenValidated = ctx =>
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
                }
            };
        });

        return builder;
    }

    private static RsaSecurityKey? LoadLocalKey(JwtBearerSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.KeyPath))
        {
            return null;
        }

        var path = Path.GetFullPath(settings.KeyPath);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var key = LoadKey(path);
            return key;
        }
        catch (Exception ex)
        {
            Defaults.Logger.LogError(ex, "Error loading RSA key from {KeyPath}", path);
            throw new InvalidOperationException(
                $"Unable to load RSA JWK from JwtBearer:KeyPath '{path}'. The application will not fall back to OIDC metadata.");
        }
    }

    private static RsaSecurityKey? LoadKey(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("kty", out var keyType) ||
            !string.Equals(keyType.GetString(), "RSA", StringComparison.Ordinal))
        {
            return null;
        }

        if (!root.TryGetProperty("n", out var modulusElement) ||
            !root.TryGetProperty("e", out var exponentElement))
        {
            return null;
        }

        var modulus = modulusElement.GetString();
        var exponent = exponentElement.GetString();
        if (string.IsNullOrWhiteSpace(modulus) || string.IsNullOrWhiteSpace(exponent))
        {
            return null;
        }

        var key = new RsaSecurityKey(new RSAParameters
        {
            Modulus = Base64UrlEncoder.DecodeBytes(modulus),
            Exponent = Base64UrlEncoder.DecodeBytes(exponent)
        });

        if (root.TryGetProperty("kid", out var keyIdElement) &&
            keyIdElement.ValueKind == JsonValueKind.String)
        {
            key.KeyId = keyIdElement.GetString();
        }

        return key;
    }
}