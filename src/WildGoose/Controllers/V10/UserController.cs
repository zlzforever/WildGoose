using System.ComponentModel.DataAnnotations;
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

    /// <summary>
    /// 
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="isAdministrator">用户是否在对应机构任管理员</param>
    /// <returns></returns>
    [HttpGet("{userId}/organizations")]
    public Task<IEnumerable<OrganizationDto>> GetOrganizationsAsync([FromRoute, StringLength(36)] string userId,
        [FromQuery] bool? isAdministrator = false)
    {
        return userService.GetOrganizationsAsync(userId, isAdministrator ?? false);
    }
}