using Microsoft.Extensions.Hosting;

namespace WildGoose.Authentication.JwtBearer;

internal sealed class JwtBearerSettings
{
    public string? Authority { get; set; }
    public string? MetadataAddress { get; set; }
    public string? KeyPath { get; set; }
    public string? ValidIssuer { get; set; }
    public string? ValidAudience { get; set; }
    public bool RequireHttpsMetadata { get; set; } = true;
    public bool ValidateAudience { get; set; } = true;
    public bool ValidateIssuer { get; set; } = true;
    public bool ValidateLifetime { get; set; } = true;
    public bool ValidateIssuerSigningKey { get; set; } = true;
}
