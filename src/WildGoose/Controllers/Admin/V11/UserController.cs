using Microsoft.AspNetCore.Mvc;
using WildGoose.Application.Services.Admin.User.V11;
using WildGoose.Application.Services.Admin.User.V11.Command;
using WildGoose.Application.Services.Admin.User.V11.Dto;

namespace WildGoose.Controllers.Admin.V11;

[ApiController]
[Route("api/admin/v1.1/users")]
[Microsoft.AspNetCore.Authorization.Authorize]
public class UserController(UserAdminService userAdminService) : ControllerBase
{
    [HttpPost]
    public Task<UserDto> Add([FromBody] AddUserCommand command)
    {
        return userAdminService.AddAsync(command);
    }
}