using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WildGoose.Domain;

namespace WildGoose.Authentication.GatewayJwtBearer;

/// <summary>
///
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
        await Task.CompletedTask;
        var options = OptionsMonitor.CurrentValue;
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
            Logger.LogDebug("X-Userinfo value is {UserInfo}", json);

            using var memoryStream = new MemoryStream(json);
            var profile = JsonSerializer.Deserialize<Profile>(memoryStream, _jsonOptions.JsonSerializerOptions);
            if (profile == null)
            {
                Logger.LogInformation("Deserialize X-Userinfo value failed");
                return AuthenticateResult.NoResult();
            }

            if (!string.IsNullOrEmpty(options.Issuer))
            {
                if (!options.Issuer.Equals(profile.Iss))
                {
                    return AuthenticateResult.Fail("Issuer is invalid");
                }
            }

            if (!string.IsNullOrEmpty(options.Audience))
            {
                if (!options.Audience.Equals(profile.Aud))
                {
                    return AuthenticateResult.Fail("Audience is invalid");
                }
            }

            var now = DateTimeOffset.UtcNow;
            var notBefore = DateTimeOffset.FromUnixTimeSeconds(profile.Nbf);
            if (now < notBefore)
            {
                return AuthenticateResult.Fail("Token is not available");
            }

            var expired = DateTimeOffset.FromUnixTimeSeconds(profile.Exp);
            if (now > expired)
            {
                return AuthenticateResult.Fail("Token is expired");
            }

            var claims = new List<Claim>
            {
                new(JwtClaimTypes.Subject, profile.Sub)
            };
            if (!string.IsNullOrEmpty(profile.Name))
            {
                claims.Add(new Claim(JwtClaimTypes.Name, profile.Name));
            }

            if (!string.IsNullOrEmpty(profile.Sid))
            {
                claims.Add(new Claim(JwtClaimTypes.SessionId, profile.Sid));
            }

            if (!string.IsNullOrEmpty(profile.PhoneNumber))
            {
                claims.Add(new Claim(JwtClaimTypes.PhoneNumber, profile.PhoneNumber));
            }

            if (!string.IsNullOrEmpty(profile.FamilyName))
            {
                claims.Add(new Claim(JwtClaimTypes.FamilyName, profile.FamilyName));
            }

            if (!string.IsNullOrEmpty(profile.GivenName))
            {
                claims.Add(new Claim(JwtClaimTypes.GivenName, profile.GivenName));
            }

            if (!string.IsNullOrEmpty(profile.Email))
            {
                claims.Add(new Claim(JwtClaimTypes.Email, profile.Email));
            }

            if (!string.IsNullOrEmpty(profile.ClientId))
            {
                claims.Add(new Claim(JwtClaimTypes.ClientId, profile.ClientId));
            }

            var roles = profile.Role;
            if (roles != null)
            {
                if (roles.RootElement.ValueKind == JsonValueKind.String)
                {
                    claims.Add(new Claim(ClaimTypes.Role, roles.RootElement.ToString()));
                }
                else if (roles.RootElement.ValueKind == JsonValueKind.Array)
                {
                    var b = roles.RootElement.Deserialize<List<string>>();
                    if (b != null)
                    {
                        foreach (var v in b)
                        {
                            claims.Add(new Claim(ClaimTypes.Role, v));
                        }
                    }
                }
            }

            foreach (var scope in profile.Scope)
            {
                claims.Add(new Claim(JwtClaimTypes.Scope, scope));
            }

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            result = AuthenticateResult.Success(ticket);
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Handle X-Userinfo value failed");
            result = AuthenticateResult.Fail("Handle X-Userinfo value failed: " + e.Message);
        }

        return result;
    }

    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private sealed class Profile
    {
        /// <summary>
        ///
        /// </summary>
        [JsonPropertyName("sub")]
        public required string Sub { get; set; }

        /// <summary>
        ///
        /// </summary>
        [JsonPropertyName("client_id")]
        public string? ClientId { get; set; }

        /// <summary>
        ///
        /// </summary>
        [JsonPropertyName("given_name")]
        public string? GivenName { get; set; }

        /// <summary>
        ///
        /// </summary>
        [JsonPropertyName("family_name")]
        public string? FamilyName { get; set; }

        /// <summary>
        ///
        /// </summary>
        [JsonPropertyName("email")]
        public string? Email { get; set; }

        /// <summary>
        ///
        /// </summary>
        [JsonPropertyName("exp")]
        public int Exp { get; set; }

        /// <summary>
        ///
        /// </summary>
        [JsonPropertyName("phone_number")]
        public string? PhoneNumber { get; set; }

        /// <summary>
        ///
        /// </summary>
        [JsonPropertyName("amr")]
        public List<string>? Amr { get; set; }

        /// <summary>
        ///
        /// </summary>
        [JsonPropertyName("auth_time")]
        public int AuthTime { get; set; }

        /// <summary>
        ///
        /// </summary>
        [JsonPropertyName("idp")]
        public string? Idp { get; set; }

        /// <summary>
        ///
        /// </summary>
        [JsonPropertyName("jti")]
        public string? Jti { get; set; }

        /// <summary>
        ///
        /// </summary>
        [JsonPropertyName("scope")]
        public List<string> Scope { get; set; } = new();

        /// <summary>
        ///
        /// </summary>
        [JsonPropertyName("nbf")]
        public int Nbf { get; set; }

        /// <summary>
        ///
        /// </summary>
        [JsonPropertyName("iat")]
        public int Iat { get; set; }

        /// <summary>
        ///
        /// </summary>
        [JsonPropertyName("iss")]
        public string? Iss { get; set; }

        /// <summary>
        ///
        /// </summary>
        [JsonPropertyName("aud")]
        public string? Aud { get; set; }

        /// <summary>
        ///
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        /// <summary>
        ///
        /// </summary>
        [JsonPropertyName("sid")]
        public string? Sid { get; set; }

        /// <summary>
        ///
        /// </summary>
        [JsonPropertyName("role")]
        public JsonDocument? Role { get; set; }
    }
}