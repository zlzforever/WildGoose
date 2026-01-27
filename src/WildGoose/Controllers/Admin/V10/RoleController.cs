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
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "SUPER")]
    [HttpGet]
    public Task<PagedResult<RoleDto>> Get([FromQuery] GetRolesQuery query)
    {
        return roleAdminService.GetRolesAsync(query);
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "SUPER")]
    [HttpPost]
    public Task<string> Create([FromBody] AddRoleCommand command)
    {
        return roleAdminService.AddAsync(command);
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "SUPER")]
    [HttpDelete("{id}")]
    public async Task<string> Delete([FromRoute] DeleteRoleCommand command)
    {
        await roleAdminService.DeleteAsync(command);
        return command.Id;
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "SUPER")]
    [HttpGet("{id}")]
    public Task<RoleDto> Get([FromRoute] GetRoleQuery query)
    {
        return roleAdminService.GetAsync(query);
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "SUPER")]
    [HttpPost("{id}")]
    public async Task<string> Update([FromRoute, Required, StringLength(36)] string id,
        [FromBody] UpdateRoleCommand command)
    {
        command.Id = id;
        await roleAdminService.UpdateAsync(command);
        return id;
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "SUPER")]
    [HttpPost("{id}/statement")]
    public async Task<string> UpdateStatement([FromRoute, Required, StringLength(36)] string id,
        [FromBody] UpdateStatementCommand command)
    {
        command.Id = id;
        await roleAdminService.UpdateStatementAsync(command);
        return id;
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "SUPER")]
    [HttpPost("assignableRoles")]
    public Task AddAssignableRole([FromBody] AddAssignableRoleCommand command)
    {
        return roleAdminService.AddAssignableRoleAsync(command);
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "SUPER")]
    [HttpDelete("{id}/assignableRoles/{assignableRoleId}")]
    public Task AddAssignableRole([FromRoute] DeleteAssignableRoleCommand command)
    {
        return roleAdminService.DeleteAssignableRoleAsync(command);
    }

    [HttpGet("assignableRoles")]
    public Task<List<RoleBasicDto>> GetAssignableRoles()
    {
        return roleAdminService.GetAssignableRolesAsync();
    }
}