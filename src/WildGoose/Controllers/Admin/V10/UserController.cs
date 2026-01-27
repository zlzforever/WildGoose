using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using WildGoose.Application.User.Admin.V10;
using WildGoose.Application.User.Admin.V10.Command;
using WildGoose.Application.User.Admin.V10.Dto;
using WildGoose.Application.User.Admin.V10.Queries;
using WildGoose.Domain;

namespace WildGoose.Controllers.Admin.V10;

[ApiController]
[Route("api/admin/v1.0/users")]
[Microsoft.AspNetCore.Authorization.Authorize(Policy = "SUPER_OR_ORG_ADMIN")]
public class UserController(UserAdminService userAdminService) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<UserDto>> Get([FromQuery] GetUsersQuery query)
    {
        return userAdminService.GetAsync(query);
    }

    [HttpPost]
    public Task<UserDto> Add([FromBody] AddUserCommand command)
    {
        return userAdminService.AddAsync(command);
    }

    [HttpGet("{id}")]
    public Task<UserDetailDto> Get([FromRoute] GetUserDetailQuery query)
    {
        return userAdminService.GetAsync(query);
    }

    [HttpDelete("{id}")]
    public async Task<string> Delete([FromRoute] DeleteUserCommand command)
    {
        await userAdminService.DeleteAsync(command);
        return command.Id;
    }

    [HttpPost("{id}/enable")]
    public async Task<string> Enable([FromRoute] EnableUserCommand command)
    {
        await userAdminService.EnableAsync(command);
        return command.Id;
    }

    [HttpPost("{id}/disable")]
    public async Task<string> Disable([FromRoute] DisableUserCommand command)
    {
        await userAdminService.DisableAsync(command);
        return command.Id;
    }

    [HttpPost("{id}/picture")]
    public async Task<string> SetPicture([FromRoute] SetPictureCommand command)
    {
        await userAdminService.SetPictureAsync(command);
        return command.Id;
    }

    [HttpPost("{id}/password")]
    public async Task<string> ChangePassword([FromRoute, StringLength(36), Required] string id,
        [FromBody] ChangePasswordCommand command)
    {
        command.Id = id;
        await userAdminService.ChangePasswordAsync(command);
        return command.Id;
    }

    [HttpPost("{id}")]
    public Task<UserDto> Update([FromRoute, StringLength(36), Required] string id,
        [FromBody] UpdateUserCommand command)
    {
        command.Id = id;
        return userAdminService.UpdateAsync(command);
    }
}