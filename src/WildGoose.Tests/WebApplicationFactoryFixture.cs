using Serilog;
using Xunit;

namespace WildGoose.Tests;

public class WebApplicationFactoryFixture : IDisposable
{
    public TestWebApplicationFactory<Program> Instance { get; private set; }

    public string? Token { get; private set; }

    public WebApplicationFactoryFixture()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .CreateLogger();
        Instance = new TestWebApplicationFactory<Program>();
    }

    public void Dispose()
    {
        Instance.Dispose();
    }
}

[CollectionDefinition("WebApplication collection")]
public class WebApplicationFactoryCollection : ICollectionFixture<WebApplicationFactoryFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}