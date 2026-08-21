using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.IdentityModel.Tokens;
using WildGoose.Domain;

namespace WildGoose.Authentication;

public static class RsaSecurityKeyHelper
{
    private static readonly ConcurrentDictionary<string, RsaSecurityKey> Cache = new();

    public static RsaSecurityKey? GetRsaSecurityKey(string? keyPath)
    {
        if (string.IsNullOrWhiteSpace(keyPath))
        {
            return null;
        }

        var path = Path.GetFullPath(keyPath);
        if (Cache.TryGetValue(path, out var cachedKey))
        {
            return cachedKey;
        }

        try
        {
            var key = LoadKey(path);
            if (key == null)
            {
                return null;
            }

            Cache.TryAdd(path, key);
            return key;
        }
        catch (Exception ex)
        {
            Defaults.Logger.LogError(ex, "Error loading RSA key from {KeyPath}", path);
            return null;
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
