using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WildGoose.Authentication.Token;
using Xunit;

namespace WildGoose.Tests.Authentication;

public sealed class TokenAuthenticationLoggingTests
{
    [Fact]
    public async Task InvalidSecurityToken_DoesNotWriteCredentialsToLogs()
    {
        const string expectedToken = "expected-secret";
        const string actualToken = "actual-secret";
        var loggerProvider = new CaptureLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(logging =>
        {
            logging.ClearProviders();
            logging.AddProvider(loggerProvider);
        });

        var handler = new TokenAuthHandler(
            new StaticOptionsMonitor<TokenAuthOptions>(new TokenAuthOptions
            {
                SecurityToken = expectedToken
            }),
            loggerFactory,
            UrlEncoder.Default);
        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "trace-token-test"
        };
        httpContext.Request.Headers["X-AUTH-TOKEN"] = actualToken;
        httpContext.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(),
            "token-test"));

        await handler.InitializeAsync(
            new AuthenticationScheme("SecurityToken", null, typeof(TokenAuthHandler)),
            httpContext);
        var result = await handler.AuthenticateAsync();

        Assert.False(result.Succeeded);
        var logText = string.Join('\n', loggerProvider.Messages);
        Assert.DoesNotContain(expectedToken, logText, StringComparison.Ordinal);
        Assert.DoesNotContain(actualToken, logText, StringComparison.Ordinal);
    }

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
        where T : class
    {
        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable OnChange(Action<T, string?> listener) => NoopDisposable.Instance;
    }

    private sealed class CaptureLoggerProvider : ILoggerProvider
    {
        public List<string> Messages { get; } = [];

        public ILogger CreateLogger(string categoryName) => new CaptureLogger(Messages);

        public void Dispose()
        {
        }
    }

    private sealed class CaptureLogger(ICollection<string> messages) : ILogger
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NoopDisposable.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            messages.Add(formatter(state, exception));
        }
    }

    private sealed class NoopDisposable : IDisposable
    {
        public static NoopDisposable Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
