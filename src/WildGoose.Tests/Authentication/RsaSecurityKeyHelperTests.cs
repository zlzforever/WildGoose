using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using WildGoose.Authentication;
using Xunit;

namespace WildGoose.Tests.Authentication;

public sealed class RsaSecurityKeyHelperTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "wildgoose-jwt-tests",
        Guid.NewGuid().ToString("N"));

    public RsaSecurityKeyHelperTests()
    {
        Directory.CreateDirectory(_directory);
    }

    [Fact]
    public void GetRsaSecurityKey_LoadsPublicJwkAndPreservesKeyId()
    {
        using var rsa = RSA.Create(2048);
        var path = WriteJwk(rsa, includePrivateParameters: false);

        var key = RsaSecurityKeyHelper.GetRsaSecurityKey(path);

        Assert.NotNull(key);
        Assert.Equal("test-key", key.KeyId);
        Assert.NotNull(key.Parameters.Modulus);
        Assert.NotNull(key.Parameters.Exponent);
        Assert.Null(key.Parameters.D);
    }

    [Fact]
    public void GetRsaSecurityKey_AcceptsExistingPrivateJwkFormat()
    {
        using var rsa = RSA.Create(2048);
        var path = WriteJwk(rsa, includePrivateParameters: true);

        var key = RsaSecurityKeyHelper.GetRsaSecurityKey(path);

        Assert.NotNull(key);
        Assert.NotNull(key.Parameters.Modulus);
        Assert.NotNull(key.Parameters.Exponent);
    }

    [Theory]
    [InlineData("{\"kty\":\"EC\",\"n\":\"AQ\",\"e\":\"AQAB\"}")]
    [InlineData("{\"kty\":\"RSA\",\"e\":\"AQAB\"}")]
    [InlineData("{\"kty\":\"RSA\",\"n\":\"not-base64\",\"e\":\"AQAB\"}")]
    public void GetRsaSecurityKey_ReturnsNullForInvalidJwk(string json)
    {
        var path = Path.Combine(_directory, Guid.NewGuid().ToString("N") + ".jwk");
        File.WriteAllText(path, json);

        var key = RsaSecurityKeyHelper.GetRsaSecurityKey(path);

        Assert.Null(key);
    }

    [Fact]
    public void GetRsaSecurityKey_DoesNotCacheAFailedLoad()
    {
        var path = Path.Combine(_directory, Guid.NewGuid().ToString("N") + ".jwk");
        File.WriteAllText(path, "{}");

        Assert.Null(RsaSecurityKeyHelper.GetRsaSecurityKey(path));

        using var rsa = RSA.Create(2048);
        File.WriteAllText(path, CreateJwk(rsa, includePrivateParameters: false));

        Assert.NotNull(RsaSecurityKeyHelper.GetRsaSecurityKey(path));
    }

    [Fact]
    public void GetRsaSecurityKey_ReturnsNullForMissingFile()
    {
        var path = Path.Combine(_directory, "missing.jwk");

        Assert.Null(RsaSecurityKeyHelper.GetRsaSecurityKey(path));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private string WriteJwk(RSA rsa, bool includePrivateParameters)
    {
        var path = Path.Combine(_directory, Guid.NewGuid().ToString("N") + ".jwk");
        File.WriteAllText(path, CreateJwk(rsa, includePrivateParameters));
        return path;
    }

    private static string CreateJwk(RSA rsa, bool includePrivateParameters)
    {
        var parameters = rsa.ExportParameters(includePrivateParameters);
        var jwk = new Dictionary<string, string?>
        {
            ["kty"] = "RSA",
            ["kid"] = "test-key",
            ["n"] = Base64UrlEncoder.Encode(parameters.Modulus),
            ["e"] = Base64UrlEncoder.Encode(parameters.Exponent)
        };

        if (includePrivateParameters)
        {
            jwk["d"] = Base64UrlEncoder.Encode(parameters.D);
            jwk["p"] = Base64UrlEncoder.Encode(parameters.P);
            jwk["q"] = Base64UrlEncoder.Encode(parameters.Q);
            jwk["dp"] = Base64UrlEncoder.Encode(parameters.DP);
            jwk["dq"] = Base64UrlEncoder.Encode(parameters.DQ);
            jwk["qi"] = Base64UrlEncoder.Encode(parameters.InverseQ);
        }

        return JsonSerializer.Serialize(jwk);
    }
}
