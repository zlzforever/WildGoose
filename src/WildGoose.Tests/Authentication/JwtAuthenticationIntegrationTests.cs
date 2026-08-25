using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using WildGoose.Authentication;
using WildGoose.Domain;
using Xunit;

namespace WildGoose.Tests.Authentication;

[Collection("WebApplication collection")]
public sealed class JwtAuthenticationIntegrationTests(WebApplicationFactoryFixture fixture) : BaseTests, IDisposable
{
    private static string TestJwkPath => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../jwt.jwk"));

    private AuthenticationTestApplication? _application;
    private RsaSecurityKey? _signingKey;

    private HttpClient _client => _application?.Client ??
                                  throw new InvalidOperationException("The test application has not started.");

    [Fact]
    public async Task ValidBearerToken_AllowsScopeAndRolePolicies()
    {
        StartApplication();

        var token = CreateToken([new Claim("scope", "openid wildgoose-api"), new Claim("role", Defaults.AdminRole)]);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/scope");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var scopeResponse = await _client!.SendAsync(request);

        using var superRequest = new HttpRequestMessage(HttpMethod.Get, "/super");
        superRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var superResponse = await _client.SendAsync(superRequest);

        Assert.Equal(HttpStatusCode.OK, scopeResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, superResponse.StatusCode);
    }

    [Fact]
    public async Task ValidBearerToken_AllowsBareAuthorizeEndpoint()
    {
        StartApplication();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/bare");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken([new Claim("scope", "wildgoose-api")]));

        var response = await _client!.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task MissingBearerToken_Returns401()
    {
        StartApplication();

        var response = await _client!.GetAsync("/bare");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WrongSignature_Returns401WithoutEchoingToken()
    {
        StartApplication();
        using var wrongRsa = RSA.Create(2048);
        var token = CreateToken(
            [
                new Claim("scope", "wildgoose-api"),
                new Claim("secret", "challenge-secret-value"),
                new Claim("private-key", "challenge-private-key-value"),
                new Claim("path", "/internal/jwt-secret/path")
            ],
            signingKey: new RsaSecurityKey(wrongRsa));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/bare");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client!.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain(token, body, StringComparison.Ordinal);
        AssertSafeBearerChallenge(
            response,
            token,
            "challenge-secret-value",
            "challenge-private-key-value",
            "/internal/jwt-secret/path");
    }

    [Fact]
    public async Task ExpiredToken_Returns401WithoutDetailedChallenge()
    {
        StartApplication();
        var token = CreateToken(
            [
                new Claim("scope", "wildgoose-api"),
                new Claim("secret", "expired-secret-value"),
                new Claim("private-key", "expired-private-key-value"),
                new Claim("path", "/internal/expired-jwt/path")
            ],
            expires: DateTime.UtcNow.AddMinutes(-5));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/bare");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client!.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        AssertSafeBearerChallenge(
            response,
            token,
            "expired-secret-value",
            "expired-private-key-value",
            "/internal/expired-jwt/path");
    }

    [Fact]
    public async Task FutureNotBefore_Returns401()
    {
        StartApplication();
        var token = CreateToken(
            [new Claim("scope", "wildgoose-api")],
            expires: DateTime.UtcNow.AddMinutes(20),
            notBefore: DateTime.UtcNow.AddMinutes(10));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/bare");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client!.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UnsignedToken_Returns401()
    {
        StartApplication();
        var token = new JwtSecurityToken(
            "https://issuer.example",
            "wildgoose-api",
            [new Claim("scope", "wildgoose-api")],
            DateTime.UtcNow.AddMinutes(-1),
            DateTime.UtcNow.AddMinutes(10));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/bare");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            new JwtSecurityTokenHandler().WriteToken(token));
        var response = await _client!.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task HmacSignedToken_Returns401()
    {
        StartApplication();
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes("not-a-rsa-signing-key-with-at-least-256-bits"));
        var token = new JwtSecurityToken(
            "https://issuer.example",
            "wildgoose-api",
            [new Claim("scope", "wildgoose-api")],
            DateTime.UtcNow.AddMinutes(-1),
            DateTime.UtcNow.AddMinutes(10),
            new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/bare");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            new JwtSecurityTokenHandler().WriteToken(token));
        var response = await _client!.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("Basic abc")]
    [InlineData("Bearer")]
    [InlineData("NotBearer abc")]
    public async Task NonBearerOrMalformedAuthorization_Returns401(string authorization)
    {
        StartApplication();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/bare");
        request.Headers.TryAddWithoutValidation("Authorization", authorization);

        var response = await _client!.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("wrong-issuer", "wildgoose-api", 0)]
    [InlineData("https://issuer.example", "wrong-audience", 0)]
    [InlineData("https://issuer.example", "wildgoose-api", -3600)]
    public async Task InvalidIssuerAudienceOrLifetime_Returns401(
        string issuer,
        string audience,
        int expirationOffsetSeconds)
    {
        StartApplication();
        var token = CreateToken(
            [new Claim("scope", "wildgoose-api")],
            issuer,
            audience,
            expirationOffsetSeconds == 0 ? null : DateTime.UtcNow.AddSeconds(expirationOffsetSeconds));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/bare");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client!.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ValidTokenWithoutScope_Returns403()
    {
        StartApplication();
        var token = CreateToken([new Claim("role", Defaults.AdminRole)]);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/scope");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client!.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ValidTokenWithoutRequiredRole_Returns403()
    {
        StartApplication();
        var token = CreateToken([new Claim("scope", "wildgoose-api"), new Claim("role", "ordinary-user")]);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/super");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client!.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    public void Dispose()
    {
        _application?.Dispose();
    }

    private void StartApplication()
    {
        _signingKey = RsaSecurityKeyHelper.GetRsaSecurityKey(TestJwkPath) ??
                      throw new InvalidOperationException($"Unable to load test JWK from '{TestJwkPath}'.");
        _application = AuthenticationTestApplication.Create(
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
    }

    private string CreateToken(
        IEnumerable<Claim> claims,
        string issuer = "https://issuer.example",
        string audience = "wildgoose-api",
        DateTime? expires = null,
        SecurityKey? signingKey = null,
        DateTime? notBefore = null)
    {
        var effectiveSigningKey = signingKey ?? _signingKey ??
                                  throw new InvalidOperationException("The signing key has not been loaded.");
        var effectiveExpiration = expires ?? DateTime.UtcNow.AddMinutes(10);
        var effectiveNotBefore = notBefore ?? (effectiveExpiration < DateTime.UtcNow
            ? effectiveExpiration.AddMinutes(-10)
            : DateTime.UtcNow.AddMinutes(-1));
        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            effectiveNotBefore,
            effectiveExpiration,
            new SigningCredentials(effectiveSigningKey, SecurityAlgorithms.RsaSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static void AssertSafeBearerChallenge(
        HttpResponseMessage response,
        string token,
        params string[] forbiddenValues)
    {
        var challengeHeaders = response.Headers.WwwAuthenticate
            .Select(header => header.ToString())
            .ToArray();
        var challenge = string.Join("\n", challengeHeaders);

        Assert.Contains(
            challengeHeaders,
            header => header.StartsWith("Bearer", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("error_description", challenge, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(token, challenge, StringComparison.Ordinal);
        foreach (var forbiddenValue in forbiddenValues)
        {
            Assert.DoesNotContain(forbiddenValue, challenge, StringComparison.Ordinal);
        }
    }
}
