using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace WildGoose.Tests.Authentication;

internal sealed class AuthenticationTestApplication : IAsyncDisposable
{
    private readonly WebApplicationFactory<WildGoose.Program> _factory;
    private readonly IReadOnlyDictionary<string, string?> _previousEnvironmentVariables;

    private AuthenticationTestApplication(
        WebApplicationFactory<WildGoose.Program> factory,
        HttpClient client,
        IReadOnlyDictionary<string, string?> previousEnvironmentVariables)
    {
        _factory = factory;
        Client = client;
        _previousEnvironmentVariables = previousEnvironmentVariables;
    }

    public HttpClient Client { get; }

    public static AuthenticationTestApplication Create(
        WebApplicationFactoryFixture fixture,
        string? schemes,
        IReadOnlyDictionary<string, string?>? overrides = null)
    {
        var environmentVariables = new Dictionary<string, string?>
        {
            ["ApiName"] = "wildgoose-api",
            ["AuthenticationSchemes"] = schemes ?? "",
            ["JwtBearer:RequireHttpsMetadata"] = "true"
        };
        if (overrides != null)
        {
            foreach (var pair in overrides)
            {
                environmentVariables[pair.Key] = pair.Value;
            }
        }

        var previousEnvironmentVariables = environmentVariables.Keys
            .ToDictionary(key => ToEnvironmentVariableName(key), Environment.GetEnvironmentVariable);
        foreach (var pair in environmentVariables)
        {
            Environment.SetEnvironmentVariable(ToEnvironmentVariableName(pair.Key), pair.Value);
        }

        var factory = fixture.Instance.WithWebHostBuilder(builder =>
        {
            builder.UseSetting(WebHostDefaults.EnvironmentKey, Environments.Production);
            builder.ConfigureTestServices(services =>
            {
                services.AddControllers()
                    .AddApplicationPart(typeof(AuthenticationTestController).Assembly);
            });
        });

        try
        {
            return new AuthenticationTestApplication(
                factory,
                factory.CreateClient(),
                previousEnvironmentVariables);
        }
        catch
        {
            RestoreEnvironmentVariables(previousEnvironmentVariables);
            factory.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        Client.Dispose();
        _factory.Dispose();
        RestoreEnvironmentVariables(_previousEnvironmentVariables);
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    private static string ToEnvironmentVariableName(string configurationKey) =>
        configurationKey.Replace(":", "__", StringComparison.Ordinal);

    private static void RestoreEnvironmentVariables(IReadOnlyDictionary<string, string?> values)
    {
        foreach (var pair in values)
        {
            Environment.SetEnvironmentVariable(pair.Key, pair.Value);
        }
    }
}
