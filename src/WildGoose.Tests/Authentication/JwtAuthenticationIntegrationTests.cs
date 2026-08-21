using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using WildGoose.Authentication;
using WildGoose.Domain;
using Xunit;

namespace WildGoose.Tests.Authentication;

public sealed class JwtAuthenticationIntegrationTests : IAsyncDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "wildgoose-jwt-integration-tests",
        Guid.NewGuid().ToString("N"));
    private RSA? _signingRsa;
    private WebApplication? _app;
    private HttpClient? _client;

    [Fact]
    public async Task ValidBearerToken_AllowsScopeAndRolePolicies()
    {
        await StartApplicationAsync();

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
        await StartApplicationAsync();

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
        await StartApplicationAsync();

        var response = await _client!.GetAsync("/bare");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WrongSignature_Returns401WithoutEchoingToken()
    {
        await StartApplicationAsync();
        using var wrongRsa = RSA.Create(2048);
        var token = CreateToken(
            [new Claim("scope", "wildgoose-api")],
            signingRsa: wrongRsa);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/bare");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client!.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain(token, body, StringComparison.Ordinal);
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
        await StartApplicationAsync();
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
        await StartApplicationAsync();
        var token = CreateToken([new Claim("role", Defaults.AdminRole)]);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/scope");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client!.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ValidTokenWithoutRequiredRole_Returns403()
    {
        await StartApplicationAsync();
        var token = CreateToken([new Claim("scope", "wildgoose-api"), new Claim("role", "ordinary-user")]);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/super");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client!.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        if (_app != null)
        {
            await _app.DisposeAsync();
        }

        _signingRsa?.Dispose();
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private async Task StartApplicationAsync()
    {
        Directory.CreateDirectory(_directory);
        _signingRsa = RSA.Create(2048);
        var keyPath = Path.Combine(_directory, "public.jwk");
        var parameters = _signingRsa.ExportParameters(includePrivateParameters: false);
        File.WriteAllText(keyPath, JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["kty"] = "RSA",
            ["kid"] = "test-key",
            ["n"] = Base64UrlEncoder.Encode(parameters.Modulus),
            ["e"] = Base64UrlEncoder.Encode(parameters.Exponent)
        }));

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(Program).Assembly.GetName().Name,
            ContentRootPath = _directory,
            EnvironmentName = Environments.Production
        });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ApiName"] = "wildgoose-api",
            ["AuthenticationSchemes"] = "JwtBearer",
            ["JwtBearer:KeyPath"] = keyPath,
            ["JwtBearer:ValidIssuer"] = "https://issuer.example",
            ["JwtBearer:ValidAudience"] = "wildgoose-api",
            ["JwtBearer:ValidateAudience"] = "true",
            ["JwtBearer:ValidateIssuer"] = "true",
            ["JwtBearer:ValidateLifetime"] = "true"
        });
        builder.Services.ConfigAuthenticationCore(builder.Configuration, builder.Environment);

        _app = builder.Build();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapGet("/bare", () => Results.Ok("ok")).RequireAuthorization();
        _app.MapGet("/scope", () => Results.Ok("ok")).RequireAuthorization("SCOPE");
        _app.MapGet("/super", () => Results.Ok("ok")).RequireAuthorization(Defaults.SuperPolicy);
        await _app.StartAsync();
        _client = _app.GetTestClient();
    }

    private string CreateToken(
        IEnumerable<Claim> claims,
        string issuer = "https://issuer.example",
        string audience = "wildgoose-api",
        DateTime? expires = null,
        RSA? signingRsa = null)
    {
        var securityKey = new RsaSecurityKey(signingRsa ?? _signingRsa!) { KeyId = "test-key" };
        var effectiveExpiration = expires ?? DateTime.UtcNow.AddMinutes(10);
        var notBefore = effectiveExpiration < DateTime.UtcNow
            ? effectiveExpiration.AddMinutes(-10)
            : DateTime.UtcNow.AddMinutes(-1);
        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            notBefore,
            effectiveExpiration,
            new SigningCredentials(securityKey, SecurityAlgorithms.RsaSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
