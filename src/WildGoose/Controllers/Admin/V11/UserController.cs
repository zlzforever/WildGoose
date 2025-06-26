using Microsoft.AspNetCore.Mvc;
using WildGoose.Application.User.Admin.V11;
using WildGoose.Application.User.Admin.V11.Command;
using WildGoose.Application.User.Admin.V11.Dto;

namespace WildGoose.Controllers.Admin.V11;

[ApiController]
[Route("api/admin/v1.1/users")]
[Microsoft.AspNetCore.Authorization.Authorize]
public class UserController(UserAdminService userAdminService) : ControllerBase
{
    [HttpPost]
    public Task<UserDto> AddAsync([FromBody] AddUserCommand command)
    {
        return userAdminService.AddAsync(command);
    }
}