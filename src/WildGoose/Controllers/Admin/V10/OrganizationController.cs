using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using WildGoose.Application.Organization.Admin.V10;
using WildGoose.Application.Organization.Admin.V10.Command;
using WildGoose.Application.Organization.Admin.V10.Dto;
using WildGoose.Application.Organization.Admin.V10.Queries;

namespace WildGoose.Controllers.Admin.V10;

[ApiController]
[Route("api/admin/v1.0/organizations")]
[Microsoft.AspNetCore.Authorization.Authorize(Roles = "admin, organization-admin")]
public class OrganizationController(OrganizationService organizationService) : ControllerBase
{
    [HttpGet("subList")]
    public Task<List<SubOrganizationDto>> GetSubListAsync([FromQuery] GetSubListQuery query)
    {
        return organizationService.GetSubListAsync(query);
    }

    /// <summary>
    /// 添加机构
    /// </summary>
    /// <returns></returns>
    [HttpPost]
    public Task<OrganizationSimpleDto> AddAsync([FromBody] AddOrganizationCommand command)
    {
        return organizationService.AddAsync(command);
    }

    [HttpGet("{id}")]
    public Task<OrganizationDetailDto> GetAsync([FromRoute] GetDetailQuery query)
    {
        return organizationService.GetAsync(query);
    }

    [HttpDelete("{id}")]
    public Task<string> DeleteAsync([FromRoute] DeleteOrganizationCommand command)
    {
        return organizationService.DeleteAsync(command.Id);
    }

    [HttpPost("{id}")]
    public Task<OrganizationSimpleDto> UpdateAsync([FromRoute, Required, StringLength(36)] string id,
        [FromBody] UpdateOrganizationCommand command)
    {
        command.Id = id;
        return organizationService.UpdateAsync(command);
    }

    [HttpPost("{id}/administrators/{userId}")]
    public Task AddAdministratorAsync([FromRoute] AddAdministratorCommand command)
    {
        return organizationService.AddAdministratorAsync(command);
    }

    [HttpDelete("{id}/administrators/{userId}")]
    public Task AddAdministratorAsync([FromRoute] DeleteAdministratorCommand command)
    {
        return organizationService.DeleteAdministratorAsync(command);
    }
}