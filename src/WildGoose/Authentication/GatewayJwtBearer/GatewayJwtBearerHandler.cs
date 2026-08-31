using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace WildGoose.Authentication.GatewayJwtBearer;

/// <summary>
/// TODO: 优化 claims 查询性能
/// </summary>
public class GatewayJwtBearerHandler : AuthenticationHandler<GatewayJwtBearerOptions>
{
    private readonly JsonOptions _jsonOptions;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="options"></param>
    /// <param name="logger"></param>
    /// <param name="encoder"></param>
    /// <param name="clock"></param>
    /// <param name="jsonOptions"></param>
    public GatewayJwtBearerHandler(IOptionsMonitor<GatewayJwtBearerOptions> options, ILoggerFactory logger,
#pragma warning disable CS0618 // Type or member is obsolete
        UrlEncoder encoder, ISystemClock clock, IOptions<JsonOptions> jsonOptions) : base(options, logger, encoder,
        clock)
#pragma warning restore CS0618 // Type or member is obsolete
    {
        _jsonOptions = jsonOptions.Value;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="options"></param>
    /// <param name="logger"></param>
    /// <param name="encoder"></param>
    /// <param name="jsonOptions"></param>
    public GatewayJwtBearerHandler(IOptionsMonitor<GatewayJwtBearerOptions> options, ILoggerFactory logger,
        UrlEncoder encoder, IOptions<JsonOptions> jsonOptions) : base(options, logger, encoder)
    {
        _jsonOptions = jsonOptions.Value;
    }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var options = Options;
        var headerName = options.Name;
        if (!Context.Request.Headers.ContainsKey(headerName))
        {
            return AuthenticateResult.NoResult();
        }

        var base64 = Context.Request.Headers[headerName].ToString();
        if (string.IsNullOrEmpty(base64))
        {
            return AuthenticateResult.NoResult();
        }

        AuthenticateResult result;
        try
        {
            var json = Convert.FromBase64String(base64);

            using var profileDocument = JsonDocument.Parse(json);
            ValidateNumericDateClaims(profileDocument.RootElement);
            var (issuer, audiences) = ParseIssuerAndAudienceClaims(profileDocument.RootElement);

            using var memoryStream = new MemoryStream(json);
            var profile =
                JsonSerializer.Deserialize<Dictionary<string, JsonElement?>>(memoryStream,
                    _jsonOptions.JsonSerializerOptions);
            if (profile == null)
            {
                Logger.LogInformation(
                    "Deserialize X-Userinfo value failed for trace {TraceId}",
                    Context.TraceIdentifier);
                return AuthenticateResult.NoResult();
            }

            Logger.LogDebug(
                "Deserialize X-Userinfo value success for trace {TraceId}; claim count {ClaimCount}",
                Context.TraceIdentifier,
                profile.Count);

            var claims = new List<Claim>();
            Add(claims, profile, "sub", ClaimTypes.NameIdentifier);
            Add(claims, profile, ClaimTypes.NameIdentifier, ClaimTypes.NameIdentifier);
            Add(claims, profile, "role", ClaimTypes.Role);
            Add(claims, profile, ClaimTypes.Role, ClaimTypes.Role);
            Add(claims, profile, "name", ClaimTypes.Name);
            Add(claims, profile, ClaimTypes.Name, ClaimTypes.Name);
            if (issuer != null)
            {
                claims.Add(new Claim("iss", issuer));
            }

            foreach (var audience in audiences)
            {
                claims.Add(new Claim("aud", audience));
            }

            Add(claims, profile, "jti");
            Add(claims, profile, "exp");
            Add(claims, profile, "nbf");
            Add(claims, profile, "client_id");
            Add(claims, profile, "security-stamp");
            Add(claims, profile, "iat");
            Add(claims, profile, "sid");

            if (profile.TryGetValue("scope", out var jsonElement) && jsonElement != null)
            {
                foreach (var scope in GetValues(jsonElement.Value))
                {
                    foreach (var value in scope.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    {
                        claims.Add(new Claim("scope", value));
                    }
                }
            }

            if (!string.IsNullOrEmpty(options.Issuer))
            {
                var iss = claims.FirstOrDefault(x => x.Type == "iss")?.Value;
                if (!options.Issuer.Equals(iss))
                {
                    return AuthenticateResult.Fail("Issuer is invalid");
                }
            }

            if (!string.IsNullOrEmpty(options.Audience))
            {
                if (!claims.Any(x => x.Type == "aud" && x.Value == options.Audience))
                {
                    return AuthenticateResult.Fail("Audience is invalid");
                }
            }

            var now = TimeProvider.GetUtcNow();
            var nbf = claims.FirstOrDefault(x => x.Type == "nbf")?.Value;
            if (nbf != null)
            {
                var notBefore = ParseNumericDate(nbf);
                if (now < notBefore)
                {
                    return AuthenticateResult.Fail("Token is not available");
                }
            }

            var exp = claims.FirstOrDefault(x => x.Type == "exp")?.Value;
            if (exp != null)
            {
                var expired = ParseNumericDate(exp);
                if (now >= expired)
                {
                    return AuthenticateResult.Fail("Token is expired");
                }
            }

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            result = AuthenticateResult.Success(ticket);
        }
        catch (Exception e)
        {
            Logger.LogError(
                "Handle X-Userinfo value failed for trace {TraceId} ({ExceptionType})",
                Context.TraceIdentifier,
                e.GetType().Name);
            result = AuthenticateResult.Fail("Handle X-Userinfo value failed");
        }

        await Task.CompletedTask;
        return result;
    }

    private static void ValidateNumericDateClaims(JsonElement profile)
    {
        if (profile.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in profile.EnumerateObject())
        {
            if (property.Name is not ("exp" or "nbf"))
            {
                continue;
            }

            if (!seen.Add(property.Name))
            {
                throw new FormatException($"NumericDate claim '{property.Name}' must appear only once.");
            }

            if (property.Value.ValueKind != JsonValueKind.Number ||
                !decimal.TryParse(
                    property.Value.GetRawText(),
                    NumberStyles.AllowLeadingSign |
                    NumberStyles.AllowDecimalPoint |
                    NumberStyles.AllowExponent,
                    CultureInfo.InvariantCulture,
                    out _))
            {
                throw new FormatException(
                    $"NumericDate claim '{property.Name}' must be a single finite JSON number.");
            }
        }
    }

    private static (string? Issuer, IReadOnlyList<string> Audiences) ParseIssuerAndAudienceClaims(
        JsonElement profile)
    {
        if (profile.ValueKind != JsonValueKind.Object)
        {
            return (null, []);
        }

        string? issuer = null;
        var audiences = new List<string>();
        var seenClaims = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in profile.EnumerateObject())
        {
            if (property.Name is not ("iss" or "aud"))
            {
                continue;
            }

            if (!seenClaims.Add(property.Name))
            {
                throw new FormatException($"JWT claim '{property.Name}' must appear only once.");
            }

            if (property.Name == "iss")
            {
                if (property.Value.ValueKind != JsonValueKind.String)
                {
                    throw new FormatException("JWT issuer claim must be a single string.");
                }

                issuer = property.Value.GetString();
                if (issuer == null)
                {
                    throw new FormatException("JWT issuer claim must be a single string.");
                }

                continue;
            }

            switch (property.Value.ValueKind)
            {
                case JsonValueKind.String:
                    AddAudience(audiences, property.Value);
                    break;
                case JsonValueKind.Array:
                    foreach (var item in property.Value.EnumerateArray())
                    {
                        if (item.ValueKind != JsonValueKind.String)
                        {
                            throw new FormatException(
                                "JWT audience claim must be a string or an array of strings.");
                        }

                        AddAudience(audiences, item);
                    }

                    break;
                default:
                    throw new FormatException(
                        "JWT audience claim must be a string or an array of strings.");
            }
        }

        return (issuer, audiences);
    }

    private static void AddAudience(List<string> audiences, JsonElement element)
    {
        var audience = element.GetString();
        if (audience == null)
        {
            throw new FormatException("JWT audience claim must be a string or an array of strings.");
        }

        audiences.Add(audience);
    }

    private static DateTimeOffset ParseNumericDate(string value)
    {
        if (!decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
        {
            throw new FormatException("NumericDate must be a finite decimal number.");
        }

        try
        {
            var absoluteTicks = DateTimeOffset.UnixEpoch.Ticks +
                                seconds * TimeSpan.TicksPerSecond;
            if (absoluteTicks < DateTimeOffset.MinValue.Ticks ||
                absoluteTicks > DateTimeOffset.MaxValue.Ticks)
            {
                throw new FormatException("NumericDate is outside the supported date range.");
            }

            return new DateTimeOffset(
                decimal.ToInt64(decimal.Truncate(absoluteTicks)),
                TimeSpan.Zero);
        }
        catch (OverflowException exception)
        {
            throw new FormatException("NumericDate is outside the supported date range.", exception);
        }
    }

    private void Add(List<Claim> claims, Dictionary<string, JsonElement?> json, string key, string? name = null)
    {
        if (!json.TryGetValue(key, out var jsonElement))
        {
            return;
        }

        if (jsonElement == null)
        {
            return;
        }

        var property = name ?? key;
        foreach (var value in GetValues(jsonElement.Value))
        {
            claims.Add(new Claim(property, value));
        }
    }

    private static IEnumerable<string> GetValues(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                var stringValue = element.GetString();
                if (!string.IsNullOrEmpty(stringValue))
                {
                    yield return stringValue;
                }

                yield break;
            case JsonValueKind.Number:
                yield return element.GetRawText();
                yield break;
            case JsonValueKind.True:
            case JsonValueKind.False:
                yield return element.GetBoolean().ToString();
                yield break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    foreach (var value in GetValues(item))
                    {
                        yield return value;
                    }
                }

                yield break;
            case JsonValueKind.Object:
                yield return element.GetRawText();
                yield break;
        }
    }
}