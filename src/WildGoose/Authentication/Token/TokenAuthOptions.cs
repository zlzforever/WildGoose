using Microsoft.AspNetCore.Authentication;

namespace WildGoose.Authentication.Token;

public class TokenAuthOptions
    : AuthenticationSchemeOptions
{
    public string AuthenticationType { get; set; } = "Token";
    public string? SecurityToken { get; set; }
}