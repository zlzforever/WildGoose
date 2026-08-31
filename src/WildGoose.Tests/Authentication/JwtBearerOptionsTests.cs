using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using WildGoose.Authentication;
using WildGoose.Authentication.JwtBearer;
using Xunit;

namespace WildGoose.Tests.Authentication;

[Collection("WebApplication collection")]
public sealed class JwtBearerOptionsTests : BaseTests, IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "wildgoose-jwt-options-tests",
        Guid.NewGuid().ToString("N"));

    public JwtBearerOptionsTests()
    {
        Directory.CreateDirectory(_directory);
    }

    [Fact]
    public void LocalJwkMode_UsesKeyAndDisablesOidcTrustSource()
    {
        using var rsa = RSA.Create(2048);
        var keyPath = WriteJwk(rsa);
        using var provider = BuildProvider(CreateConfiguration(new Dictionary<string, string?>
        {
            ["JwtBearer:KeyPath"] = keyPath,
            ["JwtBearer:Authority"] = "https://issuer.example",
            ["JwtBearer:MetadataAddress"] = "https://issuer.example/.well-known/openid-configuration",
            ["JwtBearer:ValidIssuer"] = "https://issuer.example",
            ["JwtBearer:ValidAudience"] = "wildgoose-api"
        }), "Production");

        var options = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get("JwtBearer");

        Assert.Null(options.Authority);
        Assert.Null(options.MetadataAddress);
        Assert.Null(options.ConfigurationManager);
        Assert.NotNull(options.TokenValidationParameters.IssuerSigningKey);
        Assert.True(options.TokenValidationParameters.ValidateIssuerSigningKey);
        Assert.True(options.TokenValidationParameters.ValidateIssuer);
        Assert.True(options.TokenValidationParameters.ValidateAudience);
        Assert.True(options.TokenValidationParameters.ValidateLifetime);
        Assert.False(options.IncludeErrorDetails);
        Assert.Equal("https://issuer.example", options.TokenValidationParameters.ValidIssuer);
        Assert.Equal("wildgoose-api", options.TokenValidationParameters.ValidAudience);
        Assert.Equal(ClaimTypes.Name, options.TokenValidationParameters.NameClaimType);
        Assert.Equal(ClaimTypes.Role, options.TokenValidationParameters.RoleClaimType);
    }

    [Fact]
    public void OidcMode_ResolvesAuthorityToMetadataAddress()
    {
        using var provider = BuildProvider(CreateConfiguration(new Dictionary<string, string?>
        {
            ["JwtBearer:Authority"] = "https://issuer.example/",
            ["JwtBearer:RequireHttpsMetadata"] = "true"
        }), "Production");

        var options = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get("JwtBearer");

        Assert.Equal("https://issuer.example", options.Authority);
        Assert.Equal(
            "https://issuer.example/.well-known/openid-configuration",
            options.MetadataAddress);
        Assert.True(options.RequireHttpsMetadata);
        Assert.Null(options.TokenValidationParameters.IssuerSigningKey);
        Assert.True(options.TokenValidationParameters.ValidateIssuerSigningKey);
        Assert.Equal("wildgoose-api", options.TokenValidationParameters.ValidAudience);
    }

    [Fact]
    public void OidcMode_PrefersExplicitMetadataAddress()
    {
        using var provider = BuildProvider(CreateConfiguration(new Dictionary<string, string?>
        {
            ["JwtBearer:Authority"] = "https://issuer.example",
            ["JwtBearer:MetadataAddress"] = "https://metadata.example/configuration",
            ["JwtBearer:RequireHttpsMetadata"] = "true"
        }), "Production");

        var options = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get("JwtBearer");

        Assert.Equal("https://metadata.example/configuration", options.MetadataAddress);
        Assert.Equal("https://issuer.example", options.Authority);
    }

    [Theory]
    [InlineData("relative-metadata", "JwtBearer:MetadataAddress")]
    [InlineData("ftp://issuer.example/configuration", "JwtBearer:MetadataAddress")]
    [InlineData("https:///missing-host", "JwtBearer:MetadataAddress")]
    [InlineData("relative-authority", "JwtBearer:Authority")]
    public void OidcMode_RejectsInvalidAbsoluteHttpUris(string value, string configurationKey)
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            [configurationKey] = value,
            ["JwtBearer:MetadataAddress"] = configurationKey == "JwtBearer:Authority" ? null : value,
            ["JwtBearer:Authority"] = configurationKey == "JwtBearer:Authority" ? value : null
        });

        var exception = Assert.Throws<InvalidOperationException>(() => BuildProvider(configuration, "Production"));

        Assert.Contains(configurationKey, exception.Message, StringComparison.Ordinal);
        Assert.Contains("absolute", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("JwtBearer:ValidateIssuer")]
    [InlineData("JwtBearer:ValidateAudience")]
    [InlineData("JwtBearer:ValidateLifetime")]
    public void Production_RejectsDisabledJwtSecurityValidation(string configurationKey)
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["JwtBearer:Authority"] = "https://issuer.example",
            [configurationKey] = "false"
        });

        var exception = Assert.Throws<InvalidOperationException>(() => BuildProvider(configuration, "Production"));

        Assert.Contains("Production JwtBearer configuration", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyValidAudience_FallsBackToApiName()
    {
        using var provider = BuildProvider(CreateConfiguration(new Dictionary<string, string?>
        {
            ["JwtBearer:Authority"] = "https://issuer.example",
            ["JwtBearer:ValidAudience"] = ""
        }), "Production");

        var options = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get("JwtBearer");

        Assert.Equal("wildgoose-api", options.TokenValidationParameters.ValidAudience);
    }

    [Fact]
    public void EmptyValidAudienceAndApiName_FailsConfiguration()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["JwtBearer:Authority"] = "https://issuer.example"
        });
        var builder = services.AddAuthentication();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddJwtBearerAuthentication(
                builder,
                configuration,
                "",
                new TestHostEnvironment(Environments.Production, _directory)));

        Assert.Contains("ValidAudience", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RelativeKeyPath_FallsBackToApplicationBaseDirectory()
    {
        using var rsa = RSA.Create(2048);
        var relativeName = $"wildgoose-relative-{Guid.NewGuid():N}.jwk";
        var applicationBasePath = Path.Combine(AppContext.BaseDirectory, relativeName);
        File.WriteAllText(applicationBasePath, JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["kty"] = "RSA",
            ["kid"] = "relative-test-key",
            ["n"] = Base64UrlEncoder.Encode(rsa.ExportParameters(false).Modulus),
            ["e"] = Base64UrlEncoder.Encode(rsa.ExportParameters(false).Exponent)
        }));

        try
        {
            using var provider = BuildProvider(CreateConfiguration(new Dictionary<string, string?>
            {
                ["JwtBearer:KeyPath"] = relativeName,
                ["JwtBearer:Authority"] = "https://issuer.example"
            }), "Production");

            var options = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get("JwtBearer");

            Assert.NotNull(options.TokenValidationParameters.IssuerSigningKey);
            Assert.Equal("relative-test-key", options.TokenValidationParameters.IssuerSigningKey!.KeyId);
        }
        finally
        {
            File.Delete(applicationBasePath);
        }
    }

    [Theory]
    [InlineData("http://issuer.example/.well-known/openid-configuration", "true")]
    [InlineData("http://issuer.example/.well-known/openid-configuration", "false")]
    [InlineData("https://issuer.example/.well-known/openid-configuration", "false")]
    public void Production_RejectsInsecureMetadataConfiguration(string metadataAddress, string requireHttpsMetadata)
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["JwtBearer:MetadataAddress"] = metadataAddress,
            ["JwtBearer:RequireHttpsMetadata"] = requireHttpsMetadata
        });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            BuildProvider(configuration, "Production"));

        Assert.Contains("JwtBearer", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("token", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Development_AllowsExplicitHttpMetadataException()
    {
        using var provider = BuildProvider(CreateConfiguration(new Dictionary<string, string?>
        {
            ["JwtBearer:MetadataAddress"] = "http://issuer.example/.well-known/openid-configuration",
            ["JwtBearer:RequireHttpsMetadata"] = "false"
        }), "Development");

        var options = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get("JwtBearer");

        Assert.False(options.RequireHttpsMetadata);
        Assert.StartsWith("http://", options.MetadataAddress, StringComparison.Ordinal);
    }

    [Fact]
    public void Development_RejectsHttpsMetadataWhenRequireHttpsMetadataIsDisabled()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["JwtBearer:MetadataAddress"] = "https://issuer.example/.well-known/openid-configuration",
            ["JwtBearer:RequireHttpsMetadata"] = "false"
        });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            BuildProvider(configuration, "Development"));

        Assert.Contains("RequireHttpsMetadata", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingTrustSource_FailsBeforeServiceProviderBuild()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            BuildProvider(CreateConfiguration(new Dictionary<string, string?>()), "Production"));

        Assert.Contains("Authority", exception.Message, StringComparison.Ordinal);
        Assert.Contains("MetadataAddress", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BrokenLocalKey_DoesNotFallBackToOidc()
    {
        var keyPath = Path.Combine(_directory, "broken.jwk");
        File.WriteAllText(keyPath, "{\"kty\":\"RSA\",\"n\":\"broken\",\"e\":\"AQAB\"}");
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["JwtBearer:KeyPath"] = keyPath,
            ["JwtBearer:Authority"] = "https://issuer.example"
        });

        var exception = Assert.Throws<InvalidOperationException>(() => BuildProvider(configuration, "Production"));

        Assert.Contains(keyPath, exception.Message, StringComparison.Ordinal);
        Assert.Contains("fall back", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TokenValidated_NormalizesAllScopesRolesAndName()
    {
        using var rsa = RSA.Create(2048);
        var keyPath = WriteJwk(rsa);
        using var provider = BuildProvider(CreateConfiguration(new Dictionary<string, string?>
        {
            ["JwtBearer:KeyPath"] = keyPath
        }), "Production");
        var options = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get("JwtBearer");
        var identity = new ClaimsIdentity("JwtBearer");
        identity.AddClaim(new Claim("scope", "openid wildgoose-api"));
        identity.AddClaim(new Claim("scope", "profile"));
        identity.AddClaim(new Claim("role", "admin"));
        identity.AddClaim(new Claim("name", "alice"));
        var context = new TokenValidatedContext(
            new DefaultHttpContext(),
            new AuthenticationScheme("JwtBearer", null, typeof(JwtBearerHandler)),
            options)
        {
            Principal = new ClaimsPrincipal(identity)
        };

        await options.Events.TokenValidated(context);

        Assert.Equal(
            ["openid", "wildgoose-api", "profile"],
            context.Principal!.FindAll("scope").Select(x => x.Value).ToArray());
        Assert.Contains(context.Principal.Claims, x => x.Type == ClaimTypes.Role && x.Value == "admin");
        Assert.Contains(context.Principal.Claims, x => x.Type == ClaimTypes.Name && x.Value == "alice");
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private ServiceProvider BuildProvider(IConfiguration configuration, string environmentName)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.ConfigAuthenticationCore(configuration, new TestHostEnvironment(environmentName, _directory));
        return services.BuildServiceProvider(validateScopes: true);
    }

    private string WriteJwk(RSA rsa)
    {
        var parameters = rsa.ExportParameters(includePrivateParameters: false);
        var path = Path.Combine(_directory, Guid.NewGuid().ToString("N") + ".jwk");
        File.WriteAllText(path, JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["kty"] = "RSA",
            ["kid"] = "test-key",
            ["n"] = Base64UrlEncoder.Encode(parameters.Modulus),
            ["e"] = Base64UrlEncoder.Encode(parameters.Exponent)
        }));
        return path;
    }

    private static IConfiguration CreateConfiguration(Dictionary<string, string?> overrides)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["ApiName"] = "wildgoose-api",
            ["AuthenticationSchemes"] = "JwtBearer",
            ["JwtBearer:RequireHttpsMetadata"] = "true",
            ["JwtBearer:ValidateAudience"] = "true",
            ["JwtBearer:ValidateIssuer"] = "true",
            ["JwtBearer:ValidateLifetime"] = "true",
            ["JwtBearer:ValidIssuer"] = "https://issuer.example"
        };
        foreach (var pair in overrides)
        {
            values[pair.Key] = pair.Value;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private sealed class TestHostEnvironment(string environmentName, string contentRootPath) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = typeof(Program).Assembly.GetName().Name!;
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
