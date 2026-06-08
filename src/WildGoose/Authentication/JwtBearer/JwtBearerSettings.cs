using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace WildGoose.Authentication.JwtBearer;

public class JwtBearerSettings
{
    public static string JwtBearerRsaSecurityKey = "JwtBearerRsaSecurityKey";
    public string? Authority { get; set; }
    public bool RequireHttpsMetadata { get; set; } = true;
    public bool ValidateAudience { get; set; } = true;
    public bool ValidateIssuer { get; set; } = true;
    public string? ValidIssuer { get; set; }
    public bool ValidateLifetime { get; set; } = true;
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public string? MetadataAddress { get; set; }
    public string? KeyPath { get; set; }

    /// <summary>
    /// Authority: https  && RequireHttpsMetadata: true ->  MetadataAddress: ""
    /// Authority: https  && RequireHttpsMetadata: false ->  MetadataAddress: "http://"
    /// Authority: http  && RequireHttpsMetadata: true ->  MetadataAddress: ""
    /// Authority: http  && RequireHttpsMetadata: true ->  MetadataAddress: "http://"
    /// </summary>
    /// <returns></returns>
    public string GetMetadataAddress()
    {
        if (string.IsNullOrEmpty(MetadataAddress) &&
            !string.IsNullOrEmpty(Authority))
        {
            var authority = Authority.Replace("http://", string.Empty).Replace("https://", string.Empty).TrimEnd("/");
            var schema = RequireHttpsMetadata ? "https" : "http";
            return $"{schema}://{authority}/.well-known/openid-configuration";
        }

        throw new ArgumentException("Authority or MetadataAddress cannot be null or empty.");
    }

    public RsaSecurityKey? GetRsaSecurityKey()
    {
        if (string.IsNullOrEmpty(KeyPath))
        {
            return null;
        }

        var json = File.ReadAllText(KeyPath);
        var key = JsonSerializer.Deserialize<RSAParametersInfo>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        return key?.GetRsaSecurityKey();
    }
}