using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using WildGoose.Application.Role.Admin.V10;
using WildGoose.Application.Role.Admin.V10.Command;
using WildGoose.Application.Role.Admin.V10.Dto;
using WildGoose.Application.Role.Admin.V10.Queries;
using WildGoose.Domain;

namespace WildGoose.Controllers.Admin.V10;

[ApiController]
[Route("api/admin/v1.0/roles")]
[Microsoft.AspNetCore.Authorization.Authorize]
public class RoleController(RoleService roleService) : ControllerBase
{
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "admin")]
    [HttpGet]
    public Task<PagedResult<RoleDto>> GetAsync([FromQuery] GetRolesQuery query)
    {
        return roleService.GetRolesAsync(query);
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "admin")]
    [HttpPost]
    public Task<string> CreateAsync([FromBody] AddRoleCommand command)
    {
        return roleService.AddAsync(command);
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "admin")]
    [HttpDelete("{id}")]
    public async Task<string> DeleteAsync([FromRoute] DeleteRoleCommand command)
    {
        await roleService.DeleteAsync(command);
        return command.Id;
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "admin")]
    [HttpGet("{id}")]
    public Task<RoleDto> GetAsync([FromRoute] GetRoleQuery query)
    {
        return roleService.GetAsync(query);
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "admin")]
    [HttpPost("{id}")]
    public async Task<string> UpdateAsync([FromRoute, Required, StringLength(36)] string id,
        [FromBody] UpdateRoleCommand command)
    {
        command.Id = id;
        await roleService.UpdateAsync(command);
        return id;
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "admin")]
    [HttpPost("{id}/statement")]
    public async Task<string> UpdateStatementAsync([FromRoute, Required, StringLength(36)] string id,
        [FromBody] UpdateStatementCommand command)
    {
        command.Id = id;
        await roleService.UpdateStatementAsync(command);
        return id;
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "admin")]
    [HttpPost("assignableRoles")]
    public Task AddAssignableRoleAsync([FromBody] AddAssignableRoleCommand command)
    {
        return roleService.AddAssignableRoleAsync(command);
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "admin")]
    [HttpDelete("{id}/assignableRoles/{assignableRoleId}")]
    public Task AddAssignableRoleAsync([FromRoute] DeleteAssignableRoleCommand command)
    {
        return roleService.DeleteAssignableRoleAsync(command);
    }

    [HttpGet("assignableRoles")]
    public Task<List<RoleBasicDto>> GetAssignableRolesAsync()
    {
        return roleService.GetAssignableRolesAsync();
    }
}