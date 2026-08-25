using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using WildGoose;
using Xunit;

namespace WildGoose.Tests.Authentication;

[Collection("WebApplication collection")]
public sealed class ProgramStartupTests : BaseTests, IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "wildgoose-program-startup-tests",
        Guid.NewGuid().ToString("N"));

    public ProgramStartupTests()
    {
        Directory.CreateDirectory(_directory);
    }

    [Theory]
    [InlineData("Production", "http", "true", false)]
    [InlineData("Production", "http", "false", false)]
    [InlineData("Development", "http", "false", true)]
    [InlineData("Production", "https", "true", true)]
    [InlineData("Production", "https", "false", false)]
    public void ProgramBuilder_ValidatesMetadataBeforeHostBuild(
        string environmentName,
        string metadataScheme,
        string requireHttpsMetadata,
        bool shouldBuild)
    {
        using var metadataListener = new TcpListener(IPAddress.Loopback, 0);
        metadataListener.Start();
        var metadataAddress =
            $"{metadataScheme}://127.0.0.1:{((IPEndPoint)metadataListener.LocalEndpoint).Port}/.well-known/openid-configuration";
        WriteSettings(metadataAddress, requireHttpsMetadata);

        var exception = Record.Exception(() =>
        {
            var builder = InvokeProgramBuilder(environmentName);
            if (!shouldBuild)
            {
                Assert.Fail("The unsafe metadata configuration was accepted by the Program startup path.");
            }

            using var provider = builder.Services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get("JwtBearer");
            Assert.Equal(metadataAddress, options.MetadataAddress);
            Assert.Equal(bool.Parse(requireHttpsMetadata), options.RequireHttpsMetadata);
        });

        if (shouldBuild)
        {
            Assert.Null(exception);
        }
        else
        {
            Assert.NotNull(exception);
            Assert.IsType<InvalidOperationException>(exception);
            Assert.False(metadataListener.Pending());
        }
    }

    [Fact]
    public void ProgramBuilder_DerivesMetadataAddressFromAuthority()
    {
        WriteSettings(null, "true");

        var builder = InvokeProgramBuilder("Production");
        using var provider = builder.Services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get("JwtBearer");

        Assert.Equal(
            "https://issuer.example/.well-known/openid-configuration",
            options.MetadataAddress);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private WebApplicationBuilder InvokeProgramBuilder(string environmentName)
    {
        var method = typeof(Program).GetMethod(
            "CreateBuilder",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(WebApplicationOptions)],
            modifiers: null);
        Assert.NotNull(method);

        try
        {
            return (WebApplicationBuilder)method.Invoke(null,
                [new WebApplicationOptions
                {
                    ApplicationName = typeof(Program).Assembly.GetName().Name,
                    Args = [],
                    ContentRootPath = _directory,
                    EnvironmentName = environmentName
                }])!;
        }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        {
            throw exception.InnerException;
        }
    }

    private void WriteSettings(string? metadataAddress, string requireHttpsMetadata)
    {
        var jwtBearer = new Dictionary<string, object?>
        {
            ["Authority"] = "https://issuer.example",
            ["RequireHttpsMetadata"] = bool.Parse(requireHttpsMetadata),
            ["ValidateAudience"] = true,
            ["ValidAudience"] = "wildgoose-api",
            ["ValidateIssuer"] = true,
            ["ValidIssuer"] = "https://issuer.example",
            ["ValidateLifetime"] = true
        };
        if (metadataAddress != null)
        {
            jwtBearer["MetadataAddress"] = metadataAddress;
        }

        var settings = new Dictionary<string, object?>
        {
            ["ApiName"] = "wildgoose-api",
            ["AuthenticationSchemes"] = "JwtBearer",
            ["DbContext"] = new Dictionary<string, object?>
            {
                ["DatabaseType"] = "PostgreSql",
                ["AutoMigrationEnabled"] = false,
                ["TablePrefix"] = "wild_goose_",
                ["ConnectionString"] = "Host=127.0.0.1;Port=5432;Database=test;Username=test;Password=test",
                ["TableMapper"] = new Dictionary<string, string>()
            },
            ["JwtBearer"] = jwtBearer
        };

        File.WriteAllText(
            Path.Combine(_directory, "appsettings.json"),
            JsonSerializer.Serialize(settings));
    }
}
