using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using WildGoose.Authentication;
using WildGoose.Domain;
using Xunit;

namespace WildGoose.Tests.Authentication;

[Collection("Authentication configuration")]
public sealed class AuthenticationSchemeRequestTests
{
    [Fact]
    public async Task GatewayJwtBearerAlias_UsesXUserinfoAndReturns401Or403AtRequestBoundary()
    {
        await using var application = await TestApplication.StartAsync(
            "GatewayJwtBearer",
            new Dictionary<string, string?>
            {
                ["GatewayJwtBearer:Name"] = "X-Userinfo",
                ["GatewayJwtBearer:Issuer"] = "https://issuer.example",
                ["GatewayJwtBearer:Audience"] = "wildgoose-api"
            });

        var missing = await application.Client.GetAsync("/scope");
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);

        using var malformedRequest = new HttpRequestMessage(HttpMethod.Get, "/scope");
        malformedRequest.Headers.TryAddWithoutValidation("X-Userinfo", "not-base64");
        var malformed = await application.Client.SendAsync(malformedRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, malformed.StatusCode);

        using var insufficientRequest = CreateUserinfoRequest(
            "/admin",
            new Dictionary<string, object?>
            {
                ["sub"] = "gateway-user",
                ["iss"] = "https://issuer.example",
                ["aud"] = "wildgoose-api",
                ["scope"] = "wildgoose-api",
                ["role"] = "ordinary-user"
            });
        var insufficient = await application.Client.SendAsync(insufficientRequest);
        Assert.Equal(HttpStatusCode.Forbidden, insufficient.StatusCode);

        using var validRequest = CreateUserinfoRequest(
            "/scope",
            new Dictionary<string, object?>
            {
                ["sub"] = "gateway-user",
                ["iss"] = "https://issuer.example",
                ["aud"] = "wildgoose-api",
                ["scope"] = "wildgoose-api"
            });
        var valid = await application.Client.SendAsync(validRequest);
        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
    }

    [Fact]
    public async Task GatewayLegacyConfiguration_RejectsMissingOrWrongAudience()
    {
        await using var application = await TestApplication.StartAsync(
            "GatewayJwtBearer",
            new Dictionary<string, string?>
            {
                ["GatewayJwtBearer:Name"] = "X-Legacy-Userinfo",
                ["GatewayJwtBearer:Issuer"] = "https://issuer.example",
                ["GatewayJwtBearer:Audience"] = ""
            });

        using var missingAudienceRequest = CreateUserinfoRequest(
            "/scope",
            new Dictionary<string, object?>
            {
                ["sub"] = "gateway-user",
                ["iss"] = "https://issuer.example",
                ["scope"] = "wildgoose-api"
            },
            "X-Legacy-Userinfo");
        var missingAudience = await application.Client.SendAsync(missingAudienceRequest);

        using var wrongAudienceRequest = CreateUserinfoRequest(
            "/scope",
            new Dictionary<string, object?>
            {
                ["sub"] = "gateway-user",
                ["iss"] = "https://issuer.example",
                ["aud"] = "wrong-audience",
                ["scope"] = "wildgoose-api"
            },
            "X-Legacy-Userinfo");
        var wrongAudience = await application.Client.SendAsync(wrongAudienceRequest);

        using var validRequest = CreateUserinfoRequest(
            "/scope",
            new Dictionary<string, object?>
            {
                ["sub"] = "gateway-user",
                ["iss"] = "https://issuer.example",
                ["aud"] = "wildgoose-api",
                ["scope"] = "wildgoose-api"
            },
            "X-Legacy-Userinfo");
        var valid = await application.Client.SendAsync(validRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, missingAudience.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongAudience.StatusCode);
        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
    }

    [Fact]
    public async Task SecurityToken_UsesXAuthTokenAndReturns401Or403AtRequestBoundary()
    {
        const string expectedToken = "security-token-test-value";
        var previousToken = Environment.GetEnvironmentVariable("WildGooseSecurityToken");
        Environment.SetEnvironmentVariable("WildGooseSecurityToken", expectedToken);
        try
        {
            await using var application = await TestApplication.StartAsync("SecurityToken");

            var missing = await application.Client.GetAsync("/scope");
            Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);

            using var wrongRequest = new HttpRequestMessage(HttpMethod.Get, "/scope");
            wrongRequest.Headers.Add("X-AUTH-TOKEN", "wrong-token");
            var wrong = await application.Client.SendAsync(wrongRequest);
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);

            using var insufficientRequest = new HttpRequestMessage(HttpMethod.Get, "/admin");
            insufficientRequest.Headers.Add("X-AUTH-TOKEN", expectedToken);
            var insufficient = await application.Client.SendAsync(insufficientRequest);
            Assert.Equal(HttpStatusCode.Forbidden, insufficient.StatusCode);

            using var validRequest = new HttpRequestMessage(HttpMethod.Get, "/admin");
            validRequest.Headers.Add("X-AUTH-TOKEN", expectedToken);
            validRequest.Headers.Add("X-AUTH-ROLE", Defaults.AdminRole);
            var valid = await application.Client.SendAsync(validRequest);
            Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable("WildGooseSecurityToken", previousToken);
        }
    }

    [Fact]
    public async Task MultipleSchemes_OneFailedAuthenticationDoesNotPolluteSuccessfulChallenge()
    {
        const string expectedToken = "security-token-test-value";
        var previousToken = Environment.GetEnvironmentVariable("WildGooseSecurityToken");
        Environment.SetEnvironmentVariable("WildGooseSecurityToken", expectedToken);
        try
        {
            await using var application = await TestApplication.StartAsync(
                "GatewayBearer,SecurityToken",
                new Dictionary<string, string?>
                {
                    ["GatewayBearer:Name"] = "X-Userinfo"
                });

            using var gatewayRequest = CreateUserinfoRequest(
                "/scope",
                new Dictionary<string, object?>
                {
                    ["sub"] = "gateway-user",
                    ["aud"] = "wildgoose-api",
                    ["scope"] = "wildgoose-api"
                });
            gatewayRequest.Headers.Add("X-AUTH-TOKEN", "wrong-token");
            var gatewaySuccess = await application.Client.SendAsync(gatewayRequest);
            Assert.Equal(HttpStatusCode.OK, gatewaySuccess.StatusCode);

            using var tokenRequest = new HttpRequestMessage(HttpMethod.Get, "/scope");
            tokenRequest.Headers.Add("X-AUTH-TOKEN", expectedToken);
            var tokenSuccess = await application.Client.SendAsync(tokenRequest);
            Assert.Equal(HttpStatusCode.OK, tokenSuccess.StatusCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable("WildGooseSecurityToken", previousToken);
        }
    }

    [Fact]
    public async Task MultipleSchemes_MissingCredentialsReturnsUnauthorizedChallenge()
    {
        const string expectedToken = "security-token-test-value";
        var previousToken = Environment.GetEnvironmentVariable("WildGooseSecurityToken");
        Environment.SetEnvironmentVariable("WildGooseSecurityToken", expectedToken);
        try
        {
            await using var application = await TestApplication.StartAsync(
                "GatewayBearer,SecurityToken",
                new Dictionary<string, string?>
                {
                    ["GatewayBearer:Name"] = "X-Userinfo"
                });

            var response = await application.Client.GetAsync("/scope");
            var challengeHeaders = string.Join("\n", response.Headers.WwwAuthenticate);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.DoesNotContain(expectedToken, challengeHeaders, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("WildGooseSecurityToken", previousToken);
        }
    }

    [Fact]
    public async Task JwtAndSecurityToken_MissingCredentialsReturnsBearerChallengeWithoutCredentials()
    {
        const string expectedToken = "security-token-test-value";
        var previousToken = Environment.GetEnvironmentVariable("WildGooseSecurityToken");
        Environment.SetEnvironmentVariable("WildGooseSecurityToken", expectedToken);
        try
        {
            await using var application = await TestApplication.StartAsync(
                "JwtBearer,SecurityToken",
                new Dictionary<string, string?>
                {
                    ["JwtBearer:Authority"] = "https://issuer.example",
                    ["JwtBearer:ValidateAudience"] = "true",
                    ["JwtBearer:ValidateIssuer"] = "true",
                    ["JwtBearer:ValidateLifetime"] = "true"
                });

            var response = await application.Client.GetAsync("/scope");
            var challengeHeaders = response.Headers.WwwAuthenticate.ToArray();

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            var bearerChallenge = Assert.Single(
                challengeHeaders,
                header => string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase));
            Assert.Equal("Bearer", bearerChallenge.Scheme);
            Assert.DoesNotContain(expectedToken, string.Join("\n", challengeHeaders), StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("WildGooseSecurityToken", previousToken);
        }
    }

    [Fact]
    public async Task BlankAuthenticationSchemes_UsesJwtOnlyAndDoesNotEnableXAuthToken()
    {
        const string expectedToken = "security-token-test-value";
        var previousToken = Environment.GetEnvironmentVariable("WildGooseSecurityToken");
        Environment.SetEnvironmentVariable("WildGooseSecurityToken", expectedToken);
        try
        {
            await using var application = await TestApplication.StartAsync(
                "   ",
                new Dictionary<string, string?>
                {
                    ["JwtBearer:Authority"] = "https://issuer.example",
                    ["JwtBearer:ValidateAudience"] = "true",
                    ["JwtBearer:ValidateIssuer"] = "true",
                    ["JwtBearer:ValidateLifetime"] = "true"
                });

            using var request = new HttpRequestMessage(HttpMethod.Get, "/scope");
            request.Headers.Add("X-AUTH-TOKEN", expectedToken);
            var response = await application.Client.SendAsync(request);
            var challengeHeaders = response.Headers.WwwAuthenticate.ToArray();

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Contains(
                challengeHeaders,
                header => string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Environment.SetEnvironmentVariable("WildGooseSecurityToken", previousToken);
        }
    }

    [Fact]
    public async Task CommaOnlyAuthenticationSchemes_FailsConfiguration()
    {
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            TestApplication.StartAsync(" , , "));

        Assert.Contains("at least one", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TrimmedDuplicateJwtAliases_KeepJwtAsTheDefaultScheme()
    {
        await using var application = await TestApplication.StartAsync(
            " JwtBearer , Bearer , JWTBEARER ",
            new Dictionary<string, string?>
            {
                ["JwtBearer:Authority"] = "https://issuer.example",
                ["JwtBearer:ValidateAudience"] = "true",
                ["JwtBearer:ValidateIssuer"] = "true",
                ["JwtBearer:ValidateLifetime"] = "true"
            });

        var response = await application.Client.GetAsync("/scope");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(
            response.Headers.WwwAuthenticate,
            header => string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task JwtDefaultWinsWhenExplicitSchemesAppearFirst()
    {
        const string expectedToken = "security-token-test-value";
        var previousToken = Environment.GetEnvironmentVariable("WildGooseSecurityToken");
        Environment.SetEnvironmentVariable("WildGooseSecurityToken", expectedToken);
        try
        {
            await using var application = await TestApplication.StartAsync(
                "SecurityToken, JwtBearer",
                new Dictionary<string, string?>
                {
                    ["JwtBearer:Authority"] = "https://issuer.example",
                    ["JwtBearer:ValidateAudience"] = "true",
                    ["JwtBearer:ValidateIssuer"] = "true",
                    ["JwtBearer:ValidateLifetime"] = "true"
                });

            var response = await application.Client.GetAsync("/scope");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Contains(
                response.Headers.WwwAuthenticate,
                header => string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Environment.SetEnvironmentVariable("WildGooseSecurityToken", previousToken);
        }
    }

    private static HttpRequestMessage CreateUserinfoRequest(
        string path,
        Dictionary<string, object?> profile,
        string headerName = "X-Userinfo")
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        var json = JsonSerializer.Serialize(profile);
        request.Headers.Add(headerName, Convert.ToBase64String(Encoding.UTF8.GetBytes(json)));
        return request;
    }

    [CollectionDefinition("Authentication configuration", DisableParallelization = true)]
    public sealed class AuthenticationConfigurationCollection;

    private sealed class TestApplication : IAsyncDisposable
    {
        private readonly WebApplication _app;

        private TestApplication(WebApplication app)
        {
            _app = app;
            Client = app.GetTestClient();
        }

        public HttpClient Client { get; }

        public static async Task<TestApplication> StartAsync(
            string schemes,
            IReadOnlyDictionary<string, string?>? overrides = null)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ApplicationName = typeof(Program).Assembly.GetName().Name,
                Args = [],
                EnvironmentName = Environments.Production
            });
            builder.WebHost.UseTestServer();

            var values = new Dictionary<string, string?>
            {
                ["ApiName"] = "wildgoose-api",
                ["AuthenticationSchemes"] = schemes
            };
            if (overrides != null)
            {
                foreach (var pair in overrides)
                {
                    values[pair.Key] = pair.Value;
                }
            }

            builder.Configuration.AddInMemoryCollection(values);
            builder.Services.ConfigAuthenticationCore(builder.Configuration, builder.Environment);
            var app = builder.Build();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapGet("/scope", () => Results.Ok("ok")).RequireAuthorization("SCOPE");
            app.MapGet("/admin", () => Results.Ok("ok")).RequireAuthorization(Defaults.SuperPolicy);
            await app.StartAsync();
            return new TestApplication(app);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.DisposeAsync();
        }
    }
}
