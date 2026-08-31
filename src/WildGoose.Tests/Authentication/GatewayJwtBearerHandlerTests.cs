using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WildGoose.Authentication.GatewayJwtBearer;
using Xunit;

namespace WildGoose.Tests.Authentication;

[Collection("WebApplication collection")]
public sealed class GatewayJwtBearerHandlerTests : BaseTests
{
    [Fact]
    public async Task Handler_UsesNamedOptionsForHeaderIssuerAndAudience()
    {
        var defaultOptions = new GatewayJwtBearerOptions
        {
            Name = "X-Userinfo",
            Issuer = "https://default-issuer.example",
            Audience = "wildgoose-api"
        };
        var namedOptions = new GatewayJwtBearerOptions
        {
            Name = "X-Legacy-Userinfo",
            Issuer = "https://configured-issuer.example",
            Audience = "configured-audience"
        };
        using var loggerFactory = LoggerFactory.Create(logging => logging.ClearProviders());
        var handler = CreateHandler(
            new NamedOptionsMonitor<GatewayJwtBearerOptions>(
                defaultOptions,
                new Dictionary<string, GatewayJwtBearerOptions>
                {
                    ["GatewayBearer"] = namedOptions
                }),
            loggerFactory);
        var context = CreateContext(
            "X-Legacy-Userinfo",
            new Dictionary<string, object?>
            {
                ["sub"] = "gateway-user",
                ["iss"] = "https://configured-issuer.example",
                ["aud"] = "configured-audience",
                ["scope"] = "wildgoose-api"
            });

        await handler.InitializeAsync(
            new AuthenticationScheme("GatewayBearer", null, typeof(GatewayJwtBearerHandler)),
            context);
        var result = await handler.AuthenticateAsync();

        Assert.True(result.Succeeded);
        Assert.Equal("GatewayBearer", result.Ticket!.AuthenticationScheme);
    }

    [Fact]
    public async Task Handler_LogsTraceWithoutSerializingUserinfoProfile()
    {
        var loggerProvider = new CaptureLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(logging =>
        {
            logging.ClearProviders();
            logging.SetMinimumLevel(LogLevel.Trace);
            logging.AddProvider(loggerProvider);
        });
        var options = new GatewayJwtBearerOptions
        {
            Name = "X-Userinfo",
            Issuer = "https://issuer.example",
            Audience = "wildgoose-api"
        };
        var profile = new Dictionary<string, object?>
        {
            ["sub"] = "gateway-user",
            ["iss"] = "https://issuer.example",
            ["aud"] = "wildgoose-api",
            ["scope"] = "wildgoose-api",
            ["access_token"] = "gateway-token-value",
            ["client_secret"] = "gateway-secret-value",
            ["private-key"] = "gateway-private-key-value",
            ["password"] = "gateway-password-value"
        };
        var context = CreateContext("X-Userinfo", profile, "trace-gateway-log-test");
        var handler = CreateHandler(
            new NamedOptionsMonitor<GatewayJwtBearerOptions>(options, new Dictionary<string, GatewayJwtBearerOptions>()),
            loggerFactory);

        await handler.InitializeAsync(
            new AuthenticationScheme("GatewayBearer", null, typeof(GatewayJwtBearerHandler)),
            context);
        var result = await handler.AuthenticateAsync();
        var logText = string.Join('\n', loggerProvider.Messages);

        Assert.True(result.Succeeded);
        Assert.Equal(2, loggerProvider.Messages.Count);
        Assert.Equal(
            "Deserialize X-Userinfo value success for trace trace-gateway-log-test; claim count 8",
            loggerProvider.Messages[0]);
        Assert.StartsWith(
            "AuthenticationScheme: GatewayBearer was ",
            loggerProvider.Messages[1],
            StringComparison.Ordinal);
        Assert.All(
            loggerProvider.Messages,
            message => Assert.DoesNotContain("access_token", message, StringComparison.Ordinal));
        foreach (var forbidden in new[]
                 {
                     "gateway-token-value",
                     "gateway-secret-value",
                     "gateway-private-key-value",
                     "gateway-password-value",
                     "access_token",
                     "client_secret",
                     "private-key",
                     "password"
                 })
        {
            Assert.DoesNotContain(forbidden, logText, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Handler_PreservesTypedAllowlistedClaimsAndValidatesLifetime()
    {
        var now = DateTimeOffset.UtcNow;
        var options = new GatewayJwtBearerOptions
        {
            Name = "X-Userinfo",
            Issuer = "https://issuer.example",
            Audience = "wildgoose-api"
        };
        var handler = CreateHandler(
            new NamedOptionsMonitor<GatewayJwtBearerOptions>(options, new Dictionary<string, GatewayJwtBearerOptions>()),
            LoggerFactory.Create(logging => logging.ClearProviders()));
        var context = CreateContext(
            "X-Userinfo",
            new Dictionary<string, object?>
            {
                ["sub"] = "gateway-user",
                ["name"] = "Alice",
                ["iss"] = "https://issuer.example",
                ["aud"] = "wildgoose-api",
                ["scope"] = "wildgoose-api",
                ["exp"] = now.AddMinutes(5).ToUnixTimeSeconds(),
                ["nbf"] = now.AddMinutes(-5).ToUnixTimeSeconds(),
                ["client_id"] = 42,
                ["security-stamp"] = true,
                ["sid"] = new[] { "session-a", "session-b" },
                ["jti"] = new { source = "gateway" },
                ["access_token"] = "profile-token-value",
                ["client_secret"] = "profile-secret-value",
                ["private-key"] = "profile-private-key-value",
                ["password"] = "profile-password-value"
            });

        await handler.InitializeAsync(
            new AuthenticationScheme("GatewayBearer", null, typeof(GatewayJwtBearerHandler)),
            context);
        var result = await handler.AuthenticateAsync();
        var claims = result.Ticket!.Principal.Claims.ToArray();

        Assert.True(result.Succeeded);
        Assert.Contains(claims, claim => claim.Type == ClaimTypes.Name && claim.Value == "Alice");
        Assert.Contains(claims, claim => claim.Type == "exp");
        Assert.Contains(claims, claim => claim.Type == "nbf");
        Assert.Contains(claims, claim => claim.Type == "client_id" && claim.Value == "42");
        Assert.Contains(claims, claim => claim.Type == "security-stamp" && claim.Value == "True");
        Assert.Equal(
            ["session-a", "session-b"],
            claims.Where(claim => claim.Type == "sid").Select(claim => claim.Value).ToArray());
        Assert.Contains(
            claims,
            claim => claim.Type == "jti" && claim.Value == "{\"source\":\"gateway\"}");
        Assert.DoesNotContain(
            claims,
            claim => claim.Type is "access_token" or "client_secret" or "private-key" or "password");
    }

    [Fact]
    public async Task Handler_RejectsFutureNotBeforeAndExpiredProfiles()
    {
        var options = new GatewayJwtBearerOptions
        {
            Name = "X-Userinfo",
            Issuer = "https://issuer.example",
            Audience = "wildgoose-api"
        };
        var futureHandler = CreateHandler(
            new NamedOptionsMonitor<GatewayJwtBearerOptions>(options, new Dictionary<string, GatewayJwtBearerOptions>()),
            LoggerFactory.Create(logging => logging.ClearProviders()));
        var futureContext = CreateContext(
            "X-Userinfo",
            CreateProfile(DateTimeOffset.UtcNow.AddMinutes(5), DateTimeOffset.UtcNow.AddMinutes(10)));
        await futureHandler.InitializeAsync(
            new AuthenticationScheme("GatewayBearer", null, typeof(GatewayJwtBearerHandler)),
            futureContext);

        var expiredHandler = CreateHandler(
            new NamedOptionsMonitor<GatewayJwtBearerOptions>(options, new Dictionary<string, GatewayJwtBearerOptions>()),
            LoggerFactory.Create(logging => logging.ClearProviders()));
        var expiredContext = CreateContext(
            "X-Userinfo",
            CreateProfile(DateTimeOffset.UtcNow.AddMinutes(-10), DateTimeOffset.UtcNow.AddMinutes(-5)));
        await expiredHandler.InitializeAsync(
            new AuthenticationScheme("GatewayBearer", null, typeof(GatewayJwtBearerHandler)),
            expiredContext);

        var futureResult = await futureHandler.AuthenticateAsync();
        var expiredResult = await expiredHandler.AuthenticateAsync();

        Assert.False(futureResult.Succeeded);
        Assert.False(expiredResult.Succeeded);
        Assert.Contains("not available", futureResult.Failure!.Message, StringComparison.Ordinal);
        Assert.Contains("expired", expiredResult.Failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Handler_RejectsProfileAtExactExpirationBoundary()
    {
        var expiration = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var options = new GatewayJwtBearerOptions
        {
            Name = "X-Userinfo",
            Issuer = "https://issuer.example",
            Audience = "wildgoose-api",
            TimeProvider = new FixedTimeProvider(expiration)
        };
        var handler = CreateHandler(
            new NamedOptionsMonitor<GatewayJwtBearerOptions>(options, new Dictionary<string, GatewayJwtBearerOptions>()),
            LoggerFactory.Create(logging => logging.ClearProviders()));
        var context = CreateContext(
            "X-Userinfo",
            CreateProfile(expiration.AddMinutes(-5), expiration));

        await handler.InitializeAsync(
            new AuthenticationScheme("GatewayBearer", null, typeof(GatewayJwtBearerHandler)),
            context);
        var result = await handler.AuthenticateAsync();

        Assert.False(result.Succeeded);
        Assert.Contains("expired", result.Failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Handler_AcceptsFractionalNumericDateClaims()
    {
        var options = new GatewayJwtBearerOptions
        {
            Name = "X-Userinfo",
            Issuer = "https://issuer.example",
            Audience = "wildgoose-api"
        };
        var handler = CreateHandler(
            new NamedOptionsMonitor<GatewayJwtBearerOptions>(options, new Dictionary<string, GatewayJwtBearerOptions>()),
            LoggerFactory.Create(logging => logging.ClearProviders()));
        var context = CreateContext(
            "X-Userinfo",
            new Dictionary<string, object?>
            {
                ["sub"] = "gateway-user",
                ["iss"] = "https://issuer.example",
                ["aud"] = "wildgoose-api",
                ["scope"] = "wildgoose-api",
                ["nbf"] = 0.5m,
                ["exp"] = 4102444800.5m
            });

        await handler.InitializeAsync(
            new AuthenticationScheme("GatewayBearer", null, typeof(GatewayJwtBearerHandler)),
            context);
        var result = await handler.AuthenticateAsync();

        Assert.True(result.Succeeded);
        Assert.Contains(result.Ticket!.Principal.Claims, claim => claim is
            { Type: "nbf", Value: "0.5" });
        Assert.Contains(result.Ticket.Principal.Claims, claim => claim is
            { Type: "exp", Value: "4102444800.5" });
    }

    [Fact]
    public async Task Handler_AcceptsAStringAudienceArray()
    {
        var options = new GatewayJwtBearerOptions
        {
            Name = "X-Userinfo",
            Issuer = "https://issuer.example",
            Audience = "wildgoose-api"
        };
        var handler = CreateHandler(
            new NamedOptionsMonitor<GatewayJwtBearerOptions>(options, new Dictionary<string, GatewayJwtBearerOptions>()),
            LoggerFactory.Create(logging => logging.ClearProviders()));
        var context = CreateRawContext(
            "X-Userinfo",
            "{\"sub\":\"gateway-user\",\"iss\":\"https://issuer.example\",\"aud\":[\"other-audience\",\"wildgoose-api\"],\"scope\":\"wildgoose-api\"}");

        await handler.InitializeAsync(
            new AuthenticationScheme("GatewayBearer", null, typeof(GatewayJwtBearerHandler)),
            context);
        var result = await handler.AuthenticateAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(
            ["other-audience", "wildgoose-api"],
            result.Ticket!.Principal.FindAll("aud").Select(claim => claim.Value).ToArray());
    }

    [Theory]
    [InlineData("iss", "[\"https://issuer.example\"]")]
    [InlineData("iss", "{\"value\":\"https://issuer.example\"}")]
    [InlineData("iss", "true")]
    [InlineData("iss", "null")]
    [InlineData("aud", "42")]
    [InlineData("aud", "[\"wildgoose-api\",42]")]
    [InlineData("aud", "[[\"wildgoose-api\"]]")]
    [InlineData("aud", "{\"value\":\"wildgoose-api\"}")]
    [InlineData("aud", "false")]
    [InlineData("aud", "null")]
    public async Task Handler_RejectsNonStringIssuerAndAudienceShapes(string claimName, string rawValue)
    {
        var options = new GatewayJwtBearerOptions
        {
            Name = "X-Userinfo",
            Issuer = "https://issuer.example",
            Audience = "wildgoose-api"
        };
        var handler = CreateHandler(
            new NamedOptionsMonitor<GatewayJwtBearerOptions>(options, new Dictionary<string, GatewayJwtBearerOptions>()),
            LoggerFactory.Create(logging => logging.ClearProviders()));
        var supportingClaim = claimName == "iss"
            ? "\"aud\":\"wildgoose-api\""
            : "\"iss\":\"https://issuer.example\"";
        var context = CreateRawContext(
            "X-Userinfo",
            $"{{\"sub\":\"gateway-user\",{supportingClaim},\"{claimName}\":{rawValue},\"scope\":\"wildgoose-api\"}}");

        await handler.InitializeAsync(
            new AuthenticationScheme("GatewayBearer", null, typeof(GatewayJwtBearerHandler)),
            context);
        var result = await handler.AuthenticateAsync();

        Assert.False(result.Succeeded);
    }

    [Theory]
    [InlineData("\"iss\":\"https://issuer.example\",\"iss\":\"https://issuer.example\"")]
    [InlineData("\"aud\":\"wildgoose-api\",\"aud\":\"wildgoose-api\"")]
    [InlineData("\"aud\":[\"wildgoose-api\"],\"aud\":\"wildgoose-api\"")]
    public async Task Handler_RejectsDuplicateIssuerAndAudienceClaims(string duplicateClaims)
    {
        var options = new GatewayJwtBearerOptions
        {
            Name = "X-Userinfo",
            Issuer = "https://issuer.example",
            Audience = "wildgoose-api"
        };
        var handler = CreateHandler(
            new NamedOptionsMonitor<GatewayJwtBearerOptions>(options, new Dictionary<string, GatewayJwtBearerOptions>()),
            LoggerFactory.Create(logging => logging.ClearProviders()));
        var supportingClaim = duplicateClaims.Contains("\"iss\"", StringComparison.Ordinal)
            ? "\"aud\":\"wildgoose-api\""
            : "\"iss\":\"https://issuer.example\"";
        var context = CreateRawContext(
            "X-Userinfo",
            $"{{\"sub\":\"gateway-user\",{duplicateClaims},{supportingClaim},\"scope\":\"wildgoose-api\"}}");

        await handler.InitializeAsync(
            new AuthenticationScheme("GatewayBearer", null, typeof(GatewayJwtBearerHandler)),
            context);
        var result = await handler.AuthenticateAsync();

        Assert.False(result.Succeeded);
        Assert.Equal("Handle X-Userinfo value failed", result.Failure!.Message);
    }

    [Theory]
    [InlineData("nbf", -1, true)]
    [InlineData("nbf", 0, true)]
    [InlineData("nbf", 1, false)]
    [InlineData("exp", -1, false)]
    [InlineData("exp", 0, false)]
    [InlineData("exp", 1, true)]
    public async Task Handler_UsesExactNumericDateTickBoundaries(
        string claimName,
        long deltaTicks,
        bool shouldSucceed)
    {
        var now = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero).AddTicks(1234567);
        var options = new GatewayJwtBearerOptions
        {
            Name = "X-Userinfo",
            Issuer = "https://issuer.example",
            Audience = "wildgoose-api",
            TimeProvider = new FixedTimeProvider(now)
        };
        var handler = CreateHandler(
            new NamedOptionsMonitor<GatewayJwtBearerOptions>(options, new Dictionary<string, GatewayJwtBearerOptions>()),
            LoggerFactory.Create(logging => logging.ClearProviders()));
        var context = CreateRawContext(
            "X-Userinfo",
            $"{{\"sub\":\"gateway-user\",\"iss\":\"https://issuer.example\",\"aud\":\"wildgoose-api\",\"scope\":\"wildgoose-api\",\"{claimName}\":{FormatNumericDate(now.AddTicks(deltaTicks))}}}");

        await handler.InitializeAsync(
            new AuthenticationScheme("GatewayBearer", null, typeof(GatewayJwtBearerHandler)),
            context);
        var result = await handler.AuthenticateAsync();

        Assert.Equal(shouldSucceed, result.Succeeded);
    }

    [Theory]
    [InlineData("nbf", "-62135596800", true)]
    [InlineData("exp", "253402300799.9999999", true)]
    [InlineData("nbf", "-62135596800.0000001", false)]
    [InlineData("exp", "253402300799.99999991", false)]
    public async Task Handler_UsesDecimalNumericDateRangeWithoutDoubleRounding(
        string claimName,
        string rawValue,
        bool shouldSucceed)
    {
        var now = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var options = new GatewayJwtBearerOptions
        {
            Name = "X-Userinfo",
            Issuer = "https://issuer.example",
            Audience = "wildgoose-api",
            TimeProvider = new FixedTimeProvider(now)
        };
        var handler = CreateHandler(
            new NamedOptionsMonitor<GatewayJwtBearerOptions>(options, new Dictionary<string, GatewayJwtBearerOptions>()),
            LoggerFactory.Create(logging => logging.ClearProviders()));
        var context = CreateRawContext(
            "X-Userinfo",
            $"{{\"sub\":\"gateway-user\",\"iss\":\"https://issuer.example\",\"aud\":\"wildgoose-api\",\"scope\":\"wildgoose-api\",\"{claimName}\":{rawValue}}}");

        await handler.InitializeAsync(
            new AuthenticationScheme("GatewayBearer", null, typeof(GatewayJwtBearerHandler)),
            context);
        var result = await handler.AuthenticateAsync();

        Assert.Equal(shouldSucceed, result.Succeeded);
    }

    [Theory]
    [InlineData("exp", "[4102444800,4102444801]")]
    [InlineData("exp", "[4102444801,4102444800]")]
    [InlineData("nbf", "[0,1]")]
    [InlineData("nbf", "[1,0]")]
    [InlineData("exp", "{\"seconds\":4102444800}")]
    [InlineData("nbf", "{\"seconds\":0}")]
    [InlineData("exp", "true")]
    [InlineData("nbf", "false")]
    [InlineData("exp", "null")]
    [InlineData("nbf", "null")]
    [InlineData("exp", "\"4102444800\"")]
    [InlineData("nbf", "\"0\"")]
    public async Task Handler_RejectsNonScalarNumericDateClaims(string claimName, string rawValue)
    {
        var options = new GatewayJwtBearerOptions
        {
            Name = "X-Userinfo",
            Issuer = "https://issuer.example",
            Audience = "wildgoose-api"
        };
        var handler = CreateHandler(
            new NamedOptionsMonitor<GatewayJwtBearerOptions>(options, new Dictionary<string, GatewayJwtBearerOptions>()),
            LoggerFactory.Create(logging => logging.ClearProviders()));
        var context = CreateRawContext(
            "X-Userinfo",
            $"{{\"sub\":\"gateway-user\",\"iss\":\"https://issuer.example\",\"aud\":\"wildgoose-api\",\"scope\":\"wildgoose-api\",\"{claimName}\":{rawValue}}}");

        await handler.InitializeAsync(
            new AuthenticationScheme("GatewayBearer", null, typeof(GatewayJwtBearerHandler)),
            context);
        var result = await handler.AuthenticateAsync();

        Assert.False(result.Succeeded);
        Assert.Equal("Handle X-Userinfo value failed", result.Failure!.Message);
    }

    [Theory]
    [InlineData("exp", "4102444800", "4102444801")]
    [InlineData("nbf", "0", "1")]
    public async Task Handler_RejectsDuplicateNumericDateClaims(
        string claimName,
        string firstValue,
        string secondValue)
    {
        var options = new GatewayJwtBearerOptions
        {
            Name = "X-Userinfo",
            Issuer = "https://issuer.example",
            Audience = "wildgoose-api"
        };
        var handler = CreateHandler(
            new NamedOptionsMonitor<GatewayJwtBearerOptions>(options, new Dictionary<string, GatewayJwtBearerOptions>()),
            LoggerFactory.Create(logging => logging.ClearProviders()));
        var context = CreateRawContext(
            "X-Userinfo",
            $"{{\"sub\":\"gateway-user\",\"iss\":\"https://issuer.example\",\"aud\":\"wildgoose-api\",\"scope\":\"wildgoose-api\",\"{claimName}\":{firstValue},\"{claimName}\":{secondValue}}}");

        await handler.InitializeAsync(
            new AuthenticationScheme("GatewayBearer", null, typeof(GatewayJwtBearerHandler)),
            context);
        var result = await handler.AuthenticateAsync();

        Assert.False(result.Succeeded);
        Assert.Equal("Handle X-Userinfo value failed", result.Failure!.Message);
    }

    [Theory]
    [InlineData("exp", "79228162514264337593543950335")]
    [InlineData("nbf", "-79228162514264337593543950335")]
    [InlineData("exp", "-62135596800.0000001")]
    [InlineData("nbf", "-62135596800.000001")]
    [InlineData("exp", "253402300799.99999991")]
    [InlineData("nbf", "253402300800")]
    [InlineData("exp", "\"not-a-number\"")]
    [InlineData("nbf", "\"not-a-number\"")]
    public async Task Handler_RejectsInvalidOrOutOfRangeNumericDateClaims(string claimName, string rawValue)
    {
        var options = new GatewayJwtBearerOptions
        {
            Name = "X-Userinfo",
            Issuer = "https://issuer.example",
            Audience = "wildgoose-api"
        };
        var handler = CreateHandler(
            new NamedOptionsMonitor<GatewayJwtBearerOptions>(options, new Dictionary<string, GatewayJwtBearerOptions>()),
            LoggerFactory.Create(logging => logging.ClearProviders()));
        var context = CreateRawContext(
            "X-Userinfo",
            $"{{\"sub\":\"gateway-user\",\"iss\":\"https://issuer.example\",\"aud\":\"wildgoose-api\",\"scope\":\"wildgoose-api\",\"{claimName}\":{rawValue}}}");

        await handler.InitializeAsync(
            new AuthenticationScheme("GatewayBearer", null, typeof(GatewayJwtBearerHandler)),
            context);
        var result = await handler.AuthenticateAsync();

        Assert.False(result.Succeeded);
        Assert.Equal("Handle X-Userinfo value failed", result.Failure!.Message);
    }

    [Fact]
    public async Task Handler_RejectsMissingIssuerAndWrongIssuer()
    {
        var options = new GatewayJwtBearerOptions
        {
            Name = "X-Userinfo",
            Issuer = "https://issuer.example",
            Audience = "wildgoose-api"
        };

        foreach (var json in new[]
                 {
                     "{\"sub\":\"gateway-user\",\"aud\":\"wildgoose-api\",\"scope\":\"wildgoose-api\"}",
                     "{\"sub\":\"gateway-user\",\"iss\":\"https://wrong-issuer.example\",\"aud\":\"wildgoose-api\",\"scope\":\"wildgoose-api\"}"
                 })
        {
            var handler = CreateHandler(
                new NamedOptionsMonitor<GatewayJwtBearerOptions>(
                    options,
                    new Dictionary<string, GatewayJwtBearerOptions>()),
                LoggerFactory.Create(logging => logging.ClearProviders()));
            await handler.InitializeAsync(
                new AuthenticationScheme("GatewayBearer", null, typeof(GatewayJwtBearerHandler)),
                CreateRawContext("X-Userinfo", json));

            var result = await handler.AuthenticateAsync();

            Assert.False(result.Succeeded);
            Assert.Equal("Issuer is invalid", result.Failure!.Message);
        }
    }

    [Fact]
    public async Task Handler_IgnoresNullAndMissingOptionalClaims()
    {
        var options = new GatewayJwtBearerOptions
        {
            Name = "X-Userinfo",
            Issuer = "https://issuer.example",
            Audience = "wildgoose-api"
        };
        var handler = CreateHandler(
            new NamedOptionsMonitor<GatewayJwtBearerOptions>(options, new Dictionary<string, GatewayJwtBearerOptions>()),
            LoggerFactory.Create(logging => logging.ClearProviders()));
        var context = CreateRawContext(
            "X-Userinfo",
            "{\"sub\":\"gateway-user\",\"iss\":\"https://issuer.example\",\"aud\":\"wildgoose-api\",\"scope\":null,\"role\":null,\"name\":null}");

        await handler.InitializeAsync(
            new AuthenticationScheme("GatewayBearer", null, typeof(GatewayJwtBearerHandler)),
            context);
        var result = await handler.AuthenticateAsync();
        var claims = result.Ticket!.Principal.Claims.ToArray();

        Assert.True(result.Succeeded);
        Assert.DoesNotContain(claims, claim => claim.Type is "scope" or "role" or ClaimTypes.Name);
        Assert.DoesNotContain(claims, claim => claim.Value is null);
    }

    private static DefaultHttpContext CreateRawContext(string headerName, string json)
    {
        var context = new DefaultHttpContext
        {
            TraceIdentifier = "trace-gateway-test"
        };
        context.Request.Headers[headerName] = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        return context;
    }

    private static GatewayJwtBearerHandler CreateHandler(
        IOptionsMonitor<GatewayJwtBearerOptions> options,
        ILoggerFactory loggerFactory)
    {
        return new GatewayJwtBearerHandler(
            options,
            loggerFactory,
            UrlEncoder.Default,
            Options.Create(new JsonOptions()));
    }

    private static DefaultHttpContext CreateContext(
        string headerName,
        IReadOnlyDictionary<string, object?> profile,
        string traceIdentifier = "trace-gateway-test")
    {
        var context = new DefaultHttpContext
        {
            TraceIdentifier = traceIdentifier
        };
        context.Request.Headers[headerName] = Convert.ToBase64String(
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(profile)));
        return context;
    }

    private static Dictionary<string, object?> CreateProfile(
        DateTimeOffset notBefore,
        DateTimeOffset expires)
    {
        return new Dictionary<string, object?>
        {
            ["sub"] = "gateway-user",
            ["iss"] = "https://issuer.example",
            ["aud"] = "wildgoose-api",
            ["scope"] = "wildgoose-api",
            ["nbf"] = notBefore.ToUnixTimeSeconds(),
            ["exp"] = expires.ToUnixTimeSeconds()
        };
    }

    private static string FormatNumericDate(DateTimeOffset value)
    {
        var ticksSinceEpoch = value.UtcDateTime.Ticks - DateTimeOffset.UnixEpoch.Ticks;
        return ((decimal)ticksSinceEpoch / TimeSpan.TicksPerSecond)
            .ToString(CultureInfo.InvariantCulture);
    }

    private sealed class NamedOptionsMonitor<T>(
        T defaultValue,
        IReadOnlyDictionary<string, T> namedValues) : IOptionsMonitor<T>
        where T : class
    {
        public T CurrentValue => defaultValue;

        public T Get(string? name)
        {
            return name != null && namedValues.TryGetValue(name, out var value)
                ? value
                : defaultValue;
        }

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

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
