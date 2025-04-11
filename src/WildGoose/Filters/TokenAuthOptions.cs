using Microsoft.AspNetCore.Authentication;

namespace WildGoose.Filters;

public class TokenAuthOptions
    : AuthenticationSchemeOptions
{
    public string AuthenticationType { get; set; } = "Token";
    public string Token { get; set; }
}