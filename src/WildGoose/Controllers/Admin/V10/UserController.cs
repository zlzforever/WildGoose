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
[Microsoft.AspNetCore.Authorization.Authorize(Roles = "admin, organization-admin")]
public class UserController(UserService userService) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<UserDto>> GetAsync([FromQuery] GetUsersQuery query)
    {
        return userService.GetAsync(query);
    }

    [HttpPost]
    public Task<UserDto> AddAsync([FromBody] AddUserCommand command)
    {
        return userService.AddAsync(command);
    }

    [HttpGet("{id}")]
    public Task<UserDetailDto> GetAsync([FromRoute] GetUserDetailQuery query)
    {
        return userService.GetAsync(query);
    }

    [HttpDelete("{id}")]
    public async Task<string> DeleteAsync([FromRoute] DeleteUserCommand command)
    {
        await userService.DeleteAsync(command);
        return command.Id;
    }

    [HttpPost("{id}/enable")]
    public async Task<string> EnableAsync([FromRoute] EnableUserCommand command)
    {
        await userService.EnableAsync(command);
        return command.Id;
    }

    [HttpPost("{id}/disable")]
    public async Task<string> DisableAsync([FromRoute] DisableUserCommand command)
    {
        await userService.DisableAsync(command);
        return command.Id;
    }

    [HttpPost("{id}/picture")]
    public async Task<string> SetPictureAsync([FromRoute] SetPictureCommand command)
    {
        await userService.SetPictureAsync(command);
        return command.Id;
    }

    [HttpPost("{id}/password")]
    public async Task<string> ChangePasswordAsync([FromRoute, StringLength(36), Required] string id,
        [FromBody] ChangePasswordCommand command)
    {
        command.Id = id;
        await userService.ChangePasswordAsync(command);
        return command.Id;
    }

    [HttpPost("{id}")]
    public Task<UserDto> UpdateAsync([FromRoute, StringLength(36), Required] string id,
        [FromBody] UpdateUserCommand command)
    {
        command.Id = id;
        return userService.UpdateAsync(command);
    }
}