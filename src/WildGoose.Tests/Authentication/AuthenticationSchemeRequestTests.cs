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
        await AssertSafeAuthenticationResponseAsync(missing, HttpStatusCode.Unauthorized, []);

        using var malformedRequest = new HttpRequestMessage(HttpMethod.Get, "/scope");
        malformedRequest.Headers.TryAddWithoutValidation("X-Userinfo", "not-base64");
        var malformed = await application.Client.SendAsync(malformedRequest);
        await AssertSafeAuthenticationResponseAsync(malformed, HttpStatusCode.Unauthorized, [], "not-base64");

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
        await AssertSafeAuthenticationResponseAsync(insufficient, HttpStatusCode.Forbidden, []);

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

        await AssertSafeAuthenticationResponseAsync(missingAudience, HttpStatusCode.Unauthorized, []);
        await AssertSafeAuthenticationResponseAsync(wrongAudience, HttpStatusCode.Unauthorized, [], "wrong-audience");
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
            await AssertSafeAuthenticationResponseAsync(missing, HttpStatusCode.Unauthorized, []);

            using var wrongRequest = new HttpRequestMessage(HttpMethod.Get, "/scope");
            wrongRequest.Headers.Add("X-AUTH-TOKEN", "wrong-token");
            var wrong = await application.Client.SendAsync(wrongRequest);
            await AssertSafeAuthenticationResponseAsync(wrong, HttpStatusCode.Unauthorized, [], "wrong-token");

            using var insufficientRequest = new HttpRequestMessage(HttpMethod.Get, "/super");
            insufficientRequest.Headers.Add("X-AUTH-TOKEN", expectedToken);
            var insufficient = await application.Client.SendAsync(insufficientRequest);
            await AssertSafeAuthenticationResponseAsync(insufficient, HttpStatusCode.Forbidden, [], expectedToken);

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

            await AssertSafeAuthenticationResponseAsync(response, HttpStatusCode.NotFound, [], expectedToken);
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
            await AssertSafeAuthenticationResponseAsync(
                gatewaySuccess,
                HttpStatusCode.OK,
                [],
                "wrong-token",
                "challenge-secret-value",
                "challenge-private-key-value",
                "internal exception");

            using var tokenRequest = new HttpRequestMessage(HttpMethod.Get, "/bare");
            tokenRequest.Headers.Add("X-AUTH-TOKEN", expectedToken);
            var tokenSuccess = await application.Client.SendAsync(tokenRequest);
            await AssertSafeAuthenticationResponseAsync(
                tokenSuccess,
                HttpStatusCode.OK,
                [],
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

            await AssertSafeAuthenticationResponseAsync(
                response,
                HttpStatusCode.Unauthorized,
                [],
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

            await AssertSafeAuthenticationResponseAsync(
                response,
                HttpStatusCode.Unauthorized,
                ["Bearer"],
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
        await AssertSafeAuthenticationResponseAsync(
            response,
            HttpStatusCode.Unauthorized,
            ["Bearer"],
            tokenValue,
            secret,
            privateKey,
            exceptionDetails);
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

            await AssertSafeAuthenticationResponseAsync(
                response,
                HttpStatusCode.Unauthorized,
                ["Bearer"],
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

            await AssertSafeAuthenticationResponseAsync(
                response,
                HttpStatusCode.Unauthorized,
                ["Bearer"],
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

        await AssertSafeAuthenticationResponseAsync(
            response,
            HttpStatusCode.Unauthorized,
            ["Bearer"],
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

            await AssertSafeAuthenticationResponseAsync(
                response,
                HttpStatusCode.Unauthorized,
                ["Bearer"],
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

    private static async Task AssertSafeAuthenticationResponseAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        IReadOnlyList<string> expectedChallengeSchemes,
        params string[] forbiddenValues)
    {
        Assert.Equal(expectedStatus, response.StatusCode);

        var challengeHeaders = response.Headers.WwwAuthenticate.ToArray();
        Assert.Equal(expectedChallengeSchemes.Count, challengeHeaders.Length);
        for (var index = 0; index < challengeHeaders.Length; index++)
        {
            Assert.Equal(expectedChallengeSchemes[index], challengeHeaders[index].Scheme);
            Assert.Null(challengeHeaders[index].Parameter);
        }

        var body = await response.Content.ReadAsStringAsync();
        var challenge = string.Join("\n", challengeHeaders.Select(header => header.ToString()));
        AssertNoSensitiveText($"{body}\n{challenge}", forbiddenValues);
    }

    private static void AssertNoSensitiveText(string text, params string[] forbiddenValues)
    {
        Assert.DoesNotContain("error_description", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("exception", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stack trace", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("IDX", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("detail", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("signature validation failed", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("lifetime validation failed", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("keys tried", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ValidTo", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private", text, StringComparison.OrdinalIgnoreCase);
        foreach (var forbiddenValue in forbiddenValues)
        {
            Assert.DoesNotContain(forbiddenValue, text, StringComparison.OrdinalIgnoreCase);
        }
    }

}
