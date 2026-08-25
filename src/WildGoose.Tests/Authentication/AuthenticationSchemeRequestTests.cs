using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using WildGoose.Authentication;
using WildGoose.Domain;
using Xunit;

namespace WildGoose.Tests.Authentication;

[Collection("WebApplication collection")]
public sealed class AuthenticationSchemeRequestTests(WebApplicationFactoryFixture fixture) : BaseTests
{
    private static string TestJwkPath => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../jwt.jwk"));

    [Fact]
    public async Task GatewayJwtBearerAlias_UsesXUserinfoAndReturns401Or403AtRequestBoundary()
    {
        await using var application = AuthenticationTestApplication.Create(
            fixture,
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
            "/super",
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
            "/super",
            new Dictionary<string, object?>
            {
                ["sub"] = "gateway-user",
                ["iss"] = "https://issuer.example",
                ["aud"] = "wildgoose-api",
                ["scope"] = "wildgoose-api",
                ["role"] = Defaults.AdminRole
            });
        var valid = await application.Client.SendAsync(validRequest);
        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
    }

    [Fact]
    public async Task GatewayLegacyConfiguration_RejectsMissingOrWrongAudience()
    {
        await using var application = AuthenticationTestApplication.Create(
            fixture,
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
            await using var application = AuthenticationTestApplication.Create(fixture, "SecurityToken");

            var missing = await application.Client.GetAsync("/scope");
            Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);

            using var wrongRequest = new HttpRequestMessage(HttpMethod.Get, "/scope");
            wrongRequest.Headers.Add("X-AUTH-TOKEN", "wrong-token");
            var wrong = await application.Client.SendAsync(wrongRequest);
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);

            using var insufficientRequest = new HttpRequestMessage(HttpMethod.Get, "/super");
            insufficientRequest.Headers.Add("X-AUTH-TOKEN", expectedToken);
            var insufficient = await application.Client.SendAsync(insufficientRequest);
            Assert.Equal(HttpStatusCode.Forbidden, insufficient.StatusCode);

            using var validRequest = new HttpRequestMessage(HttpMethod.Get, "/super");
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
    public async Task UnknownAdminPath_WithValidSecurityToken_ReturnsNotFound()
    {
        const string expectedToken = "security-token-test-value";
        var previousToken = Environment.GetEnvironmentVariable("WildGooseSecurityToken");
        Environment.SetEnvironmentVariable("WildGooseSecurityToken", expectedToken);
        try
        {
            await using var application = AuthenticationTestApplication.Create(fixture, "SecurityToken");

            using var request = new HttpRequestMessage(HttpMethod.Get, "/admin");
            request.Headers.Add("X-AUTH-TOKEN", expectedToken);
            request.Headers.Add("X-AUTH-ROLE", Defaults.AdminRole);
            var response = await application.Client.SendAsync(request);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Empty(response.Headers.WwwAuthenticate);
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
            await using var application = AuthenticationTestApplication.Create(
                fixture,
                "GatewayBearer,SecurityToken",
                new Dictionary<string, string?>
                {
                    ["GatewayBearer:Name"] = "X-Userinfo"
                });

            using var gatewayRequest = CreateUserinfoRequest(
                "/bare",
                new Dictionary<string, object?>
                {
                    ["sub"] = "gateway-user",
                    ["aud"] = "wildgoose-api",
                    ["scope"] = "wildgoose-api"
                });
            gatewayRequest.Headers.Add("X-AUTH-TOKEN", "wrong-token");
            var gatewaySuccess = await application.Client.SendAsync(gatewayRequest);
            Assert.Equal(HttpStatusCode.OK, gatewaySuccess.StatusCode);
            Assert.Empty(gatewaySuccess.Headers.WwwAuthenticate);
            AssertNoSensitiveChallenge(
                gatewaySuccess,
                "wrong-token",
                "challenge-secret-value",
                "challenge-private-key-value",
                "internal exception");

            using var tokenRequest = new HttpRequestMessage(HttpMethod.Get, "/bare");
            tokenRequest.Headers.Add("X-AUTH-TOKEN", expectedToken);
            var tokenSuccess = await application.Client.SendAsync(tokenRequest);
            Assert.Equal(HttpStatusCode.OK, tokenSuccess.StatusCode);
            Assert.Empty(tokenSuccess.Headers.WwwAuthenticate);
            AssertNoSensitiveChallenge(
                tokenSuccess,
                expectedToken,
                "challenge-secret-value",
                "challenge-private-key-value",
                "internal exception");
        }
        finally
        {
            Environment.SetEnvironmentVariable("WildGooseSecurityToken", previousToken);
        }
    }

    [Fact]
    public async Task MultipleSchemes_DefaultAuthorizeEndpointAcceptsSecurityToken()
    {
        const string expectedToken = "security-token-test-value";
        var previousToken = Environment.GetEnvironmentVariable("WildGooseSecurityToken");
        Environment.SetEnvironmentVariable("WildGooseSecurityToken", expectedToken);
        try
        {
            await using var application = AuthenticationTestApplication.Create(
                fixture,
                "JwtBearer,SecurityToken",
                new Dictionary<string, string?>
                {
                    ["JwtBearer:Authority"] = "https://issuer.example",
                    ["JwtBearer:ValidateAudience"] = "true",
                    ["JwtBearer:ValidateIssuer"] = "true",
                    ["JwtBearer:ValidateLifetime"] = "true"
                });

            using var request = new HttpRequestMessage(HttpMethod.Get, "/bare");
            request.Headers.Add("X-AUTH-TOKEN", expectedToken);
            var response = await application.Client.SendAsync(request);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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
            await using var application = AuthenticationTestApplication.Create(
                fixture,
                "GatewayBearer,SecurityToken",
                new Dictionary<string, string?>
                {
                    ["GatewayBearer:Name"] = "X-Userinfo"
                });

            var response = await application.Client.GetAsync("/bare");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            AssertNoSensitiveChallenge(
                response,
                expectedToken,
                "challenge-secret-value",
                "challenge-private-key-value",
                "internal exception");
            Assert.Empty(response.Headers.WwwAuthenticate);
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
            await using var application = AuthenticationTestApplication.Create(
                fixture,
                "JwtBearer,SecurityToken",
                new Dictionary<string, string?>
                {
                    ["JwtBearer:Authority"] = "https://issuer.example",
                    ["JwtBearer:ValidateAudience"] = "true",
                    ["JwtBearer:ValidateIssuer"] = "true",
                    ["JwtBearer:ValidateLifetime"] = "true"
                });

            var response = await application.Client.GetAsync("/bare");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            AssertSafeBearerChallenge(
                response,
                expectedToken,
                "challenge-secret-value",
                "challenge-private-key-value",
                "internal exception");
        }
        finally
        {
            Environment.SetEnvironmentVariable("WildGooseSecurityToken", previousToken);
        }
    }

    [Fact]
    public async Task InvalidJwtChallenge_DoesNotExposeSensitiveTokenClaims()
    {
        const string secret = "challenge-secret-value";
        const string privateKey = "challenge-private-key-value";
        const string exceptionDetails = "challenge-internal-exception-value";

        await using var application = AuthenticationTestApplication.Create(
            fixture,
            "JwtBearer",
            new Dictionary<string, string?>
            {
                ["JwtBearer:KeyPath"] = TestJwkPath,
                ["JwtBearer:ValidIssuer"] = "https://issuer.example",
                ["JwtBearer:ValidAudience"] = "wildgoose-api",
                ["JwtBearer:ValidateAudience"] = "true",
                ["JwtBearer:ValidateIssuer"] = "true",
                ["JwtBearer:ValidateLifetime"] = "true"
            });

        var invalidToken = new JwtSecurityToken(
            "https://issuer.example",
            "wildgoose-api",
            [
                new Claim("scope", "wildgoose-api"),
                new Claim("secret", secret),
                new Claim("private-key", privateKey),
                new Claim("internal-exception", exceptionDetails)
            ],
            DateTime.UtcNow.AddMinutes(-1),
            DateTime.UtcNow.AddMinutes(5));
        var tokenValue = new JwtSecurityTokenHandler().WriteToken(invalidToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/bare");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenValue);
        var response = await application.Client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        AssertSafeBearerChallenge(response, tokenValue, secret, privateKey, exceptionDetails);
        AssertNoSensitiveText(body, tokenValue, secret, privateKey, exceptionDetails);
    }

    [Fact]
    public async Task BlankAuthenticationSchemes_UsesJwtOnlyAndDoesNotEnableXAuthToken()
    {
        const string expectedToken = "security-token-test-value";
        var previousToken = Environment.GetEnvironmentVariable("WildGooseSecurityToken");
        Environment.SetEnvironmentVariable("WildGooseSecurityToken", expectedToken);
        try
        {
            await using var application = AuthenticationTestApplication.Create(
                fixture,
                "   ",
                new Dictionary<string, string?>
                {
                    ["JwtBearer:Authority"] = "https://issuer.example",
                    ["JwtBearer:ValidateAudience"] = "true",
                    ["JwtBearer:ValidateIssuer"] = "true",
                    ["JwtBearer:ValidateLifetime"] = "true"
                });

            using var request = new HttpRequestMessage(HttpMethod.Get, "/bare");
            request.Headers.Add("X-AUTH-TOKEN", expectedToken);
            var response = await application.Client.SendAsync(request);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            AssertSafeBearerChallenge(
                response,
                expectedToken,
                "challenge-secret-value",
                "challenge-private-key-value",
                "internal exception");
        }
        finally
        {
            Environment.SetEnvironmentVariable("WildGooseSecurityToken", previousToken);
        }
    }

    [Fact]
    public async Task MissingAuthenticationSchemes_UsesJwtOnlyAndDoesNotEnableXAuthToken()
    {
        const string expectedToken = "security-token-test-value";
        var previousToken = Environment.GetEnvironmentVariable("WildGooseSecurityToken");
        Environment.SetEnvironmentVariable("WildGooseSecurityToken", expectedToken);
        try
        {
            await using var application = AuthenticationTestApplication.Create(
                fixture,
                null,
                new Dictionary<string, string?>
                {
                    ["JwtBearer:Authority"] = "https://issuer.example",
                    ["JwtBearer:ValidateAudience"] = "true",
                    ["JwtBearer:ValidateIssuer"] = "true",
                    ["JwtBearer:ValidateLifetime"] = "true"
                });

            using var request = new HttpRequestMessage(HttpMethod.Get, "/bare");
            request.Headers.Add("X-AUTH-TOKEN", expectedToken);
            var response = await application.Client.SendAsync(request);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            AssertSafeBearerChallenge(
                response,
                expectedToken,
                "challenge-secret-value",
                "challenge-private-key-value",
                "internal exception");
        }
        finally
        {
            Environment.SetEnvironmentVariable("WildGooseSecurityToken", previousToken);
        }
    }

    [Fact]
    public void CommaOnlyAuthenticationSchemes_FailsConfiguration()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            AuthenticationTestApplication.Create(fixture, " , , "));

        Assert.Contains("at least one", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TrimmedDuplicateJwtAliases_KeepJwtAsTheDefaultScheme()
    {
        await using var application = AuthenticationTestApplication.Create(
            fixture,
            " JwtBearer , Bearer , JWTBEARER ",
            new Dictionary<string, string?>
            {
                ["JwtBearer:Authority"] = "https://issuer.example",
                ["JwtBearer:ValidateAudience"] = "true",
                ["JwtBearer:ValidateIssuer"] = "true",
                ["JwtBearer:ValidateLifetime"] = "true"
            });

        var response = await application.Client.GetAsync("/bare");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        AssertSafeBearerChallenge(
            response,
            "challenge-secret-value",
            "challenge-private-key-value",
            "internal exception");
    }

    [Fact]
    public async Task JwtDefaultWinsWhenExplicitSchemesAppearFirst()
    {
        const string expectedToken = "security-token-test-value";
        var previousToken = Environment.GetEnvironmentVariable("WildGooseSecurityToken");
        Environment.SetEnvironmentVariable("WildGooseSecurityToken", expectedToken);
        try
        {
            await using var application = AuthenticationTestApplication.Create(
                fixture,
                "SecurityToken, JwtBearer",
                new Dictionary<string, string?>
                {
                    ["JwtBearer:Authority"] = "https://issuer.example",
                    ["JwtBearer:ValidateAudience"] = "true",
                    ["JwtBearer:ValidateIssuer"] = "true",
                    ["JwtBearer:ValidateLifetime"] = "true"
                });

            var response = await application.Client.GetAsync("/bare");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            AssertSafeBearerChallenge(
                response,
                expectedToken,
                "challenge-secret-value",
                "challenge-private-key-value",
                "internal exception");
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

    private static void AssertSafeBearerChallenge(
        HttpResponseMessage response,
        params string[] forbiddenValues)
    {
        var challengeHeaders = response.Headers.WwwAuthenticate.ToArray();
        var bearerChallenge = Assert.Single(challengeHeaders);

        Assert.Equal("Bearer", bearerChallenge.Scheme);
        Assert.Null(bearerChallenge.Parameter);
        AssertNoSensitiveChallenge(response, forbiddenValues);
    }

    private static void AssertNoSensitiveChallenge(
        HttpResponseMessage response,
        params string[] forbiddenValues)
    {
        var challenge = string.Join(
            "\n",
            response.Headers.WwwAuthenticate.Select(header => header.ToString()));

        Assert.DoesNotContain("error_description", challenge, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("exception", challenge, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stack trace", challenge, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", challenge, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private", challenge, StringComparison.OrdinalIgnoreCase);
        foreach (var forbiddenValue in forbiddenValues)
        {
            Assert.DoesNotContain(forbiddenValue, challenge, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static void AssertNoSensitiveText(string text, params string[] forbiddenValues)
    {
        Assert.DoesNotContain("exception", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stack trace", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private", text, StringComparison.OrdinalIgnoreCase);
        foreach (var forbiddenValue in forbiddenValues)
        {
            Assert.DoesNotContain(forbiddenValue, text, StringComparison.OrdinalIgnoreCase);
        }
    }

}
