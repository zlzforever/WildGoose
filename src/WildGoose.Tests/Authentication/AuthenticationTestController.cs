using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WildGoose.Domain;

namespace WildGoose.Tests.Authentication;

[ApiController]
[Route("")]
public sealed class AuthenticationTestController : ControllerBase
{
    [HttpGet("bare")]
    [Authorize]
    public IActionResult Bare() => Ok("ok");

    [HttpGet("scope")]
    [Authorize(Policy = "SCOPE")]
    public IActionResult Scope() => Ok("ok");

    [HttpGet("super")]
    [Authorize(Policy = Defaults.SuperPolicy)]
    public IActionResult Admin() => Ok("ok");
}
