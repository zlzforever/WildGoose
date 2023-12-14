using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using WildGoose.Application;
using WildGoose.Application.Role.Admin.V10;
using WildGoose.Application.Role.Admin.V10.Command;
using WildGoose.Application.Role.Admin.V10.Dto;
using WildGoose.Application.Role.Admin.V10.Queries;
using WildGoose.Domain;

namespace WildGoose.Controllers.Admin.V10;

[ApiController]
[Route("api/admin/v1.0/roles")]
#if !DEBUG
[Microsoft.AspNetCore.Authorization.Authorize(Roles = "admin")]
#endif
public class RoleController : ControllerBase
{
    private readonly RoleService _roleService;

    public RoleController(RoleService roleService)
    {
        _roleService = roleService;
    }

    [HttpGet]
    public Task<PagedResult<RoleDto>> GetAsync([FromQuery] GetRolesQuery query)
    {
        return _roleService.GetRolesAsync(query);
    }

    [HttpPost]
    public Task<string> CreateAsync([FromBody] AddRoleCommand command)
    {
        return _roleService.AddAsync(command);
    }

    [HttpDelete("{id}")]
    public async Task<string> DeleteAsync([FromRoute] DeleteRoleCommand command)
    {
        await _roleService.DeleteAsync(command);
        return command.Id;
    }

    [HttpGet("{id}")]
    public Task<RoleDto> GetAsync([FromRoute] GetRoleQuery query)
    {
        return _roleService.GetAsync(query);
    }

    [HttpPost("{id}")]
    public async Task<string> UpdateAsync([FromRoute, Required, StringLength(36)] string id,
        [FromBody] UpdateRoleCommand command)
    {
        command.Id = id;
        await _roleService.UpdateAsync(command);
        return id;
    }

    [HttpPost("{id}/statement")]
    public async Task<string> UpdateStatementAsync([FromRoute, Required, StringLength(36)] string id,
        [FromBody] UpdateStatementCommand command)
    {
        command.Id = id;
        await _roleService.UpdateStatementAsync(command);
        return id;
    }

    [HttpPost("assignableRoles")]
    public Task AddAssignableRoleAsync([FromBody] AddAssignableRoleCommand command)
    {
        return _roleService.AddAssignableRoleAsync(command);
    }

    [HttpDelete("{id}/assignableRoles/{assignableRoleId}")]
    public Task AddAssignableRoleAsync([FromRoute] DeleteAssignableRoleCommand command)
    {
        return _roleService.DeleteAssignableRoleAsync(command);
    }

    [HttpGet("assignableRoles")]
    public Task<List<RoleBasicDto>> GetAssignableRolesAsync()
    {
        return _roleService.GetAssignableRolesAsync();
    }
}