namespace WildGoose;

public class JwtBearerSettings
{
    public static string JwtBearerRsaSecurityKey = "JwtBearerRsaSecurityKey";
    public string? Authority { get; set; }
    public bool ValidateAudience { get; set; } = true;
    public bool ValidateIssuer { get; set; } = true;
    public string? ValidIssuer { get; set; }
    public string? ValidAudience { get; set; }
    public bool ValidateLifetime { get; set; } = true;
    public RSAParametersInfo? Key { get; set; }
    public bool RequireHttpsMetadata { get; set; } = true;
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public string? MetadataAddress { get; set; }

    public string GetMetadataAddress()
    {
        // 试验性代码，authority 不设计 https/requireHttpsMetadata
        if (!RequireHttpsMetadata && string.IsNullOrEmpty(MetadataAddress) &&
            !string.IsNullOrEmpty(Authority))
        {
            var metadataAddress =
                Authority.Replace("https://", "http://", StringComparison.OrdinalIgnoreCase);
            if (!metadataAddress.EndsWith("/", StringComparison.Ordinal))
            {
                metadataAddress += "/";
            }

            return metadataAddress + ".well-known/openid-configuration";
        }

        return MetadataAddress ?? string.Empty;
    }
}