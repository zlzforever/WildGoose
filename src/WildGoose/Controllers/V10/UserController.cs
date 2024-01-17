using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WildGoose.Application.User.V10;
using WildGoose.Application.User.V10.Command;

namespace WildGoose.Controllers.V10;

[ApiController]
[Route("api/v1.0/users")]
[Authorize]
public class UserController(UserService userService) : ControllerBase
{
    [HttpPost("resetPasswordByCaptcha")]
    public Task ResetPasswordByCaptchaAsync([FromBody] ResetPasswordByCaptchaCommand command)
    {
        return userService.ResetPasswordByCaptchaAsync(command);
    }
}