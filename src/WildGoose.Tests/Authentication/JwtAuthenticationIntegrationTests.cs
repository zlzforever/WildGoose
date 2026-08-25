using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
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
    private RSA? _signingRsa;
    private CryptoProviderFactory? _signingProviderFactory;
    private InMemoryCryptoProviderCache? _signingProviderCache;

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
    public void SigningWithCachedProviderAfterPreviousRsaIsDisposed_ReproducesObjectDisposedException()
    {
        var (factory, cache) = CreateTestCryptoProviderFactory(cacheSignatureProviders: true);
        RSA? firstRsa = null;
        RSA? secondRsa = null;
        try
        {
            Assert.True(factory.CacheSignatureProviders);

            var firstSigningKey = LoadSigningKey(TestJwkPath);
            firstRsa = firstSigningKey.Rsa;
            var firstSecurityKey = CreateSigningKey(firstRsa, firstSigningKey.KeyId, factory);
            _ = WriteSignedToken(firstSecurityKey);
            firstRsa.Dispose();

            var secondSigningKey = LoadSigningKey(TestJwkPath);
            secondRsa = secondSigningKey.Rsa;
            var secondSecurityKey = CreateSigningKey(secondRsa, secondSigningKey.KeyId, factory);

            Assert.Throws<ObjectDisposedException>(() => WriteSignedToken(secondSecurityKey));
        }
        finally
        {
            try
            {
                cache.Dispose();
            }
            finally
            {
                try
                {
                    secondRsa?.Dispose();
                }
                finally
                {
                    firstRsa?.Dispose();
                }
            }
        }
    }

    [Fact]
    public void SigningWithCacheDisabledAfterPreviousRsaIsDisposed_RemainsUsable()
    {
        var (factory, cache) = CreateTestCryptoProviderFactory(cacheSignatureProviders: false);
        RSA? firstRsa = null;
        RSA? secondRsa = null;
        try
        {
            Assert.False(factory.CacheSignatureProviders);

            var firstSigningKey = LoadSigningKey(TestJwkPath);
            firstRsa = firstSigningKey.Rsa;
            var firstSecurityKey = CreateSigningKey(firstRsa, firstSigningKey.KeyId, factory);
            _ = WriteSignedToken(firstSecurityKey);
            firstRsa.Dispose();

            var secondSigningKey = LoadSigningKey(TestJwkPath);
            secondRsa = secondSigningKey.Rsa;
            var secondSecurityKey = CreateSigningKey(secondRsa, secondSigningKey.KeyId, factory);

            var token = WriteSignedToken(secondSecurityKey);

            Assert.False(string.IsNullOrWhiteSpace(token));

            var validationKey = new RsaSecurityKey(secondRsa.ExportParameters(includePrivateParameters: false))
            {
                KeyId = secondSigningKey.KeyId
            };
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = validationKey,
                ValidIssuer = "https://issuer.example",
                ValidateIssuer = true,
                ValidAudience = "wildgoose-api",
                ValidateAudience = true,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
                ValidAlgorithms = [SecurityAlgorithms.RsaSha256]
            };
            var principal = new JwtSecurityTokenHandler().ValidateToken(
                token,
                validationParameters,
                out var validatedToken);

            var validatedJwt = Assert.IsType<JwtSecurityToken>(validatedToken);
            Assert.Equal(SecurityAlgorithms.RsaSha256, validatedJwt.Header.Alg);
            Assert.Equal(secondSigningKey.KeyId, validatedJwt.Header.Kid);
            Assert.Equal("https://issuer.example", validatedJwt.Issuer);
            Assert.Contains("wildgoose-api", validatedJwt.Audiences);
            Assert.Contains(
                principal.Claims,
                claim => claim.Type == "scope" && claim.Value == "wildgoose-api");
        }
        finally
        {
            try
            {
                cache.Dispose();
            }
            finally
            {
                try
                {
                    secondRsa?.Dispose();
                }
                finally
                {
                    firstRsa?.Dispose();
                }
            }
        }
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
        var (wrongFactory, wrongCache) = CreateTestCryptoProviderFactory(cacheSignatureProviders: false);
        RSA? wrongRsa = null;
        try
        {
            Assert.False(wrongFactory.CacheSignatureProviders);
            wrongRsa = RSA.Create(2048);
            var token = CreateToken(
                [
                    new Claim("scope", "wildgoose-api"),
                    new Claim("secret", "challenge-secret-value"),
                    new Claim("private-key", "challenge-private-key-value"),
                    new Claim("path", "/internal/jwt-secret/path")
                ],
                signingKey: CreateSigningKey(wrongRsa, "wrong-signature-key", wrongFactory));

            using var request = new HttpRequestMessage(HttpMethod.Get, "/bare");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await _client!.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            AssertNoSensitiveText(
                body,
                token,
                "challenge-secret-value",
                "challenge-private-key-value",
                "/internal/jwt-secret/path");
            AssertSafeBearerChallenge(
                response,
                token,
                "challenge-secret-value",
                "challenge-private-key-value",
                "/internal/jwt-secret/path");
        }
        finally
        {
            try
            {
                wrongCache.Dispose();
            }
            finally
            {
                wrongRsa?.Dispose();
            }
        }
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
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        AssertNoSensitiveText(
            body,
            token,
            "expired-secret-value",
            "expired-private-key-value",
            "/internal/expired-jwt/path");
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
        var (hmacFactory, hmacCache) = CreateTestCryptoProviderFactory(cacheSignatureProviders: false);
        try
        {
            Assert.False(hmacFactory.CacheSignatureProviders);
            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes("not-a-rsa-signing-key-with-at-least-256-bits"))
            {
                CryptoProviderFactory = hmacFactory
            };
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
        finally
        {
            hmacCache.Dispose();
        }
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
        try
        {
            _application?.Dispose();
        }
        finally
        {
            try
            {
                _signingProviderCache?.Dispose();
            }
            finally
            {
                _signingRsa?.Dispose();
                _signingProviderFactory = null;
                _signingProviderCache = null;
                _signingRsa = null;
                _signingKey = null;
            }
        }
    }

    private void StartApplication()
    {
        var signingKey = LoadSigningKey(TestJwkPath);
        _signingRsa = signingKey.Rsa;
        (_signingProviderFactory, _signingProviderCache) =
            CreateTestCryptoProviderFactory(cacheSignatureProviders: false);
        _signingKey = CreateSigningKey(_signingRsa, signingKey.KeyId, _signingProviderFactory);
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

    private static (RSA Rsa, string KeyId) LoadSigningKey(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        var rsa = RSA.Create();
        try
        {
            rsa.ImportParameters(new RSAParameters
            {
                Modulus = ReadKeyParameter(root, "n"),
                Exponent = ReadKeyParameter(root, "e"),
                D = ReadKeyParameter(root, "d"),
                P = ReadKeyParameter(root, "p"),
                Q = ReadKeyParameter(root, "q"),
                DP = ReadKeyParameter(root, "dp"),
                DQ = ReadKeyParameter(root, "dq"),
                InverseQ = ReadKeyParameter(root, "qi")
            });

            var keyId = root.GetProperty("kid").GetString();
            if (string.IsNullOrWhiteSpace(keyId))
            {
                throw new InvalidOperationException($"Test JWK '{path}' does not define a key id.");
            }

            return (rsa, keyId);
        }
        catch
        {
            rsa.Dispose();
            throw;
        }
    }

    private static byte[] ReadKeyParameter(JsonElement root, string name)
    {
        var value = root.GetProperty(name).GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Test JWK is missing RSA parameter '{name}'.");
        }

        return Base64UrlEncoder.DecodeBytes(value);
    }

    private static (CryptoProviderFactory Factory, InMemoryCryptoProviderCache Cache)
        CreateTestCryptoProviderFactory(bool cacheSignatureProviders)
    {
        // The dedicated cache owns cached signature providers and is disposed by each caller.
        var cache = new InMemoryCryptoProviderCache();
        var factory = new CryptoProviderFactory(cache)
        {
            CacheSignatureProviders = cacheSignatureProviders
        };
        return (factory, cache);
    }

    private static RsaSecurityKey CreateSigningKey(
        RSA rsa,
        string keyId,
        CryptoProviderFactory factory)
    {
        var key = new RsaSecurityKey(rsa) { KeyId = keyId };
        key.CryptoProviderFactory = factory;
        return key;
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

    private static string WriteSignedToken(SecurityKey signingKey)
    {
        var token = new JwtSecurityToken(
            "https://issuer.example",
            "wildgoose-api",
            [new Claim("scope", "wildgoose-api")],
            DateTime.UtcNow.AddMinutes(-1),
            DateTime.UtcNow.AddMinutes(10),
            new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static void AssertSafeBearerChallenge(
        HttpResponseMessage response,
        string token,
        params string[] forbiddenValues)
    {
        var challengeHeaders = response.Headers.WwwAuthenticate.ToArray();
        var bearerChallenge = Assert.Single(challengeHeaders);

        Assert.Equal("Bearer", bearerChallenge.Scheme);
        Assert.Null(bearerChallenge.Parameter);
        AssertNoSensitiveText(
            string.Join("\n", challengeHeaders.Select(header => header.ToString())),
            [token, ..forbiddenValues]);
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
