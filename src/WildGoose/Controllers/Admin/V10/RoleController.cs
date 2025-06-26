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
public class RoleController(RoleAdminService roleAdminService) : ControllerBase
{
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "admin")]
    [HttpGet]
    public Task<PagedResult<RoleDto>> GetAsync([FromQuery] GetRolesQuery query)
    {
        return roleAdminService.GetRolesAsync(query);
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "admin")]
    [HttpPost]
    public Task<string> CreateAsync([FromBody] AddRoleCommand command)
    {
        return roleAdminService.AddAsync(command);
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "admin")]
    [HttpDelete("{id}")]
    public async Task<string> DeleteAsync([FromRoute] DeleteRoleCommand command)
    {
        await roleAdminService.DeleteAsync(command);
        return command.Id;
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "admin")]
    [HttpGet("{id}")]
    public Task<RoleDto> GetAsync([FromRoute] GetRoleQuery query)
    {
        return roleAdminService.GetAsync(query);
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "admin")]
    [HttpPost("{id}")]
    public async Task<string> UpdateAsync([FromRoute, Required, StringLength(36)] string id,
        [FromBody] UpdateRoleCommand command)
    {
        command.Id = id;
        await roleAdminService.UpdateAsync(command);
        return id;
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "admin")]
    [HttpPost("{id}/statement")]
    public async Task<string> UpdateStatementAsync([FromRoute, Required, StringLength(36)] string id,
        [FromBody] UpdateStatementCommand command)
    {
        command.Id = id;
        await roleAdminService.UpdateStatementAsync(command);
        return id;
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "admin")]
    [HttpPost("assignableRoles")]
    public Task AddAssignableRoleAsync([FromBody] AddAssignableRoleCommand command)
    {
        return roleAdminService.AddAssignableRoleAsync(command);
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "admin")]
    [HttpDelete("{id}/assignableRoles/{assignableRoleId}")]
    public Task AddAssignableRoleAsync([FromRoute] DeleteAssignableRoleCommand command)
    {
        return roleAdminService.DeleteAssignableRoleAsync(command);
    }

    [HttpGet("assignableRoles")]
    public Task<List<RoleBasicDto>> GetAssignableRolesAsync()
    {
        return roleAdminService.GetAssignableRolesAsync();
    }
}