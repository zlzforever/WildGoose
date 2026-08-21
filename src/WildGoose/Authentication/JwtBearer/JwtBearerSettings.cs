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

    public string ResolveKeyPath(IHostEnvironment environment)
    {
        if (string.IsNullOrWhiteSpace(KeyPath))
        {
            throw new InvalidOperationException("JwtBearer:KeyPath is empty.");
        }

        if (Path.IsPathRooted(KeyPath))
        {
            return Path.GetFullPath(KeyPath);
        }

        var contentRootPath = Path.GetFullPath(KeyPath, environment.ContentRootPath);
        if (File.Exists(contentRootPath))
        {
            return contentRootPath;
        }

        return Path.GetFullPath(KeyPath, AppContext.BaseDirectory);
    }
}
