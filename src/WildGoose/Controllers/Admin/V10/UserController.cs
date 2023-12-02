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
#if !DEBUG
[Authorize(Roles = "admin, organization-admin")]
#endif
public class UserController : ControllerBase
{
    private readonly UserService _userService;

    public UserController(UserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    public async Task<PagedResult<UserDto>> GetAsync([FromQuery] GetUsersQuery query)
    {
        return await _userService.GetAsync(query);
    }

    [HttpPost]
    public async Task<UserDto> AddAsync([FromBody] AddUserCommand command)
    {
        return await _userService.AddAsync(command);
    }

    [HttpGet("{id}")]
    public async Task<UserDetailDto> GetAsync([FromRoute] GetUserDetailQuery query)
    {
        return await _userService.GetAsync(query);
    }

    [HttpDelete("{id}")]
    public async Task<string> DeleteAsync([FromRoute] DeleteUserCommand command)
    {
        await _userService.DeleteAsync(command);
        return command.Id;
    }

    [HttpPost("{id}/enable")]
    public async Task<string> EnableAsync([FromRoute] EnableUserCommand command)
    {
        await _userService.EnableAsync(command);
        return command.Id;
    }

    [HttpPost("{id}/disable")]
    public async Task<string> DisableAsync([FromRoute] DisableUserCommand command)
    {
        await _userService.DisableAsync(command);
        return command.Id;
    }

    [HttpPost("{id}/picture")]
    public async Task<string> SetPictureAsync([FromRoute] SetPictureCommand command)
    {
        await _userService.SetPictureAsync(command);
        return command.Id;
    }

    [HttpPost("{id}/password")]
    public async Task<string> ChangePasswordAsync([FromRoute, StringLength(36), Required] string id,
        [FromBody] ChangePasswordCommand command)
    {
        command.Id = id;
        await _userService.ChangePasswordAsync(command);
        return command.Id;
    }

    [HttpPost("{id}")]
    public async Task<UserDto> UpdateAsync([FromRoute, StringLength(36), Required] string id,
        [FromBody] UpdateUserCommand command)
    {
        command.Id = id;
        return await _userService.UpdateAsync(command);
    }
}