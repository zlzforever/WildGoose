using Microsoft.AspNetCore.Builder;
using System.Text;
using Microsoft.Extensions.Hosting;
using WildGoose;
using Xunit;

namespace WildGoose.Tests.Authentication;

[Collection("WebApplication collection")]
public sealed class ConfigurationSubstitutionTests : BaseTests
{
    [Fact]
    public void AddSubstitution_LoadsDevelopmentConfigurationWithoutDisposedStream()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(Program).Assembly.GetName().Name,
            ContentRootPath = AppContext.BaseDirectory,
            EnvironmentName = Environments.Development
        });

        var exception = Record.Exception(builder.AddSubstitution);

        Assert.Null(exception);
        Assert.Equal("http://localhost:8099", builder.Configuration["JwtBearer:Authority"]);
    }

    [Fact]
    public void AddSubstitution_AcceptsCommentsTrailingCommasAndEmptyCollections()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "wildgoose-substitution-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "appsettings.json"), """
            {
              // Deployment files may contain comments and trailing commas.
              "JwtBearer": {
                "Authority": "https://issuer.example",
              },
              "AllowedCorsOrigins": [],
              "WildGoose": {
                "UserPropertyMappings": {},
              },
            }
            """, Encoding.UTF8);

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ApplicationName = typeof(Program).Assembly.GetName().Name,
                Args = [],
                ContentRootPath = directory,
                EnvironmentName = Environments.Production
            });

            var exception = Record.Exception(builder.AddSubstitution);

            Assert.Null(exception);
            Assert.Equal("https://issuer.example", builder.Configuration["JwtBearer:Authority"]);
            Assert.Empty(builder.Configuration.GetSection("AllowedCorsOrigins").GetChildren());
            Assert.Empty(builder.Configuration.GetSection("WildGoose:UserPropertyMappings").GetChildren());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
