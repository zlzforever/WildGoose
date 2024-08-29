using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WildGoose.Application.User.V10;
using WildGoose.Application.User.V10.Command;
using WildGoose.Application.User.V10.Dto;

namespace WildGoose.Controllers.V10;

[ApiController]
[Route("api/v1.0/users")]
[Authorize]
public class UserController(UserService userService) : ControllerBase
{
    [HttpPost("resetPasswordByCaptcha")]
    public Task ResetPasswordByCaptcha([FromBody] ResetPasswordByCaptchaCommand command)
    {
        return userService.ResetPasswordByCaptchaAsync(command);
    }

    [HttpGet("{userId}/organizations")]
    public Task<IEnumerable<OrganizationDto>> GetOrganizationsAsync([FromRoute] string userId,
        [FromQuery] bool isAdministrator = false)
    {
        return userService.GetOrganizationsAsync(userId, isAdministrator);
    }
}