using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WildGoose.Application.User.V10;
using WildGoose.Application.User.V10.Command;

namespace WildGoose.Controllers.V10;

[ApiController]
[Route("api/v1.0/users")]
#if !DEBUG
[Authorize]
#endif
public class UserController : ControllerBase
{
    private readonly UserService _userService;

    public UserController(UserService userService)
    {
        _userService = userService;
    }

    [HttpPost("resetPasswordByCaptcha")]
    public Task ResetPasswordByCaptchaAsync([FromBody] ResetPasswordByCaptchaCommand command)
    {
        return _userService.ResetPasswordByCaptchaAsync(command);
    }
}