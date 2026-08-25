using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using WildGoose.Authentication;
using Xunit;

namespace WildGoose.Tests.Authentication;

[Collection("WebApplication collection")]
public sealed class MetadataRequestTests : BaseTests
{
    [Fact]
    public async Task ProductionHttpMetadataRejectedBeforeMetadataRequest()
    {
        await using var server = new CountingMetadataServer();
        server.Start();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ApiName"] = "wildgoose-api",
            ["AuthenticationSchemes"] = "JwtBearer",
            ["JwtBearer:MetadataAddress"] = server.MetadataAddress,
            ["JwtBearer:RequireHttpsMetadata"] = "true",
            ["JwtBearer:ValidateAudience"] = "true",
            ["JwtBearer:ValidateIssuer"] = "true",
            ["JwtBearer:ValidateLifetime"] = "true",
            ["JwtBearer:ValidIssuer"] = "https://issuer.example",
            ["JwtBearer:ValidAudience"] = "wildgoose-api"
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        Exception? exception = null;

        try
        {
            services.ConfigAuthenticationCore(
                configuration,
                new TestHostEnvironment(Environments.Production));

            using var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get("JwtBearer");
            var manager = Assert.IsAssignableFrom<IConfigurationManager<OpenIdConnectConfiguration>>(
                options.ConfigurationManager);
            await manager.GetConfigurationAsync(CancellationToken.None);
        }
        catch (Exception caught)
        {
            exception = caught;
        }

        Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(0, server.RequestCount);
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = typeof(Program).Assembly.GetName().Name!;
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    private sealed class CountingMetadataServer : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _shutdown = new();
        private Task? _acceptLoop;
        private int _requestCount;

        public string MetadataAddress { get; private set; } = string.Empty;

        public int RequestCount => Volatile.Read(ref _requestCount);

        public void Start()
        {
            _listener.Start();
            var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            MetadataAddress = $"http://127.0.0.1:{port}/.well-known/openid-configuration";
            _acceptLoop = AcceptLoopAsync();
        }

        public async ValueTask DisposeAsync()
        {
            _shutdown.Cancel();
            _listener.Stop();
            if (_acceptLoop != null)
            {
                try
                {
                    await _acceptLoop;
                }
                catch (OperationCanceledException)
                {
                }
                catch (ObjectDisposedException)
                {
                }
            }

            _shutdown.Dispose();
        }

        private async Task AcceptLoopAsync()
        {
            try
            {
                while (!_shutdown.IsCancellationRequested)
                {
                    using var client = await _listener.AcceptTcpClientAsync(_shutdown.Token);
                    Interlocked.Increment(ref _requestCount);
                    using var stream = client.GetStream();
                    var body = "{\"issuer\":\"https://issuer.example\"}"u8.ToArray();
                    var headers =
                        $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n";
                    var headerBytes = System.Text.Encoding.ASCII.GetBytes(headers);
                    await stream.WriteAsync(headerBytes, _shutdown.Token);
                    await stream.WriteAsync(body, _shutdown.Token);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }
}
