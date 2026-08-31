using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace WildGoose.Tests.Authentication;

[Collection("WebApplication collection")]
public sealed class RsaSecurityKeyHelperTests : BaseTests, IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "wildgoose-jwt-tests",
        Guid.NewGuid().ToString("N"));

    public RsaSecurityKeyHelperTests()
    {
        Directory.CreateDirectory(_directory);
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