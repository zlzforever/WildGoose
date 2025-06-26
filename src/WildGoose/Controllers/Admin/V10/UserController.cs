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
public class UserController(UserAdminService userAdminService) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<UserDto>> GetAsync([FromQuery] GetUsersQuery query)
    {
        return userAdminService.GetAsync(query);
    }

    [HttpPost]
    public Task<UserDto> AddAsync([FromBody] AddUserCommand command)
    {
        return userAdminService.AddAsync(command);
    }

    [HttpGet("{id}")]
    public Task<UserDetailDto> GetAsync([FromRoute] GetUserDetailQuery query)
    {
        return userAdminService.GetAsync(query);
    }

    [HttpDelete("{id}")]
    public async Task<string> DeleteAsync([FromRoute] DeleteUserCommand command)
    {
        await userAdminService.DeleteAsync(command);
        return command.Id;
    }

    [HttpPost("{id}/enable")]
    public async Task<string> EnableAsync([FromRoute] EnableUserCommand command)
    {
        await userAdminService.EnableAsync(command);
        return command.Id;
    }

    [HttpPost("{id}/disable")]
    public async Task<string> DisableAsync([FromRoute] DisableUserCommand command)
    {
        await userAdminService.DisableAsync(command);
        return command.Id;
    }

    [HttpPost("{id}/picture")]
    public async Task<string> SetPictureAsync([FromRoute] SetPictureCommand command)
    {
        await userAdminService.SetPictureAsync(command);
        return command.Id;
    }

    [HttpPost("{id}/password")]
    public async Task<string> ChangePasswordAsync([FromRoute, StringLength(36), Required] string id,
        [FromBody] ChangePasswordCommand command)
    {
        command.Id = id;
        await userAdminService.ChangePasswordAsync(command);
        return command.Id;
    }

    [HttpPost("{id}")]
    public Task<UserDto> UpdateAsync([FromRoute, StringLength(36), Required] string id,
        [FromBody] UpdateUserCommand command)
    {
        command.Id = id;
        return userAdminService.UpdateAsync(command);
    }
}