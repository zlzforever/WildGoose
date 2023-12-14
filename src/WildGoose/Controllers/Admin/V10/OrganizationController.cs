using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using WildGoose.Application.Organization.Admin.V10;
using WildGoose.Application.Organization.Admin.V10.Command;
using WildGoose.Application.Organization.Admin.V10.Dto;
using WildGoose.Application.Organization.Admin.V10.Queries;

namespace WildGoose.Controllers.Admin.V10;

[ApiController]
[Route("api/admin/v1.0/organizations")]
#if !DEBUG
[Microsoft.AspNetCore.Authorization.Authorize(Roles = "admin, organization-admin")]
#endif
public class OrganizationController : ControllerBase
{
    private readonly OrganizationService _organizationService;

    public OrganizationController(OrganizationService organizationService)
    {
        _organizationService = organizationService;
    }

    /// <summary>
    /// 添加机构
    /// </summary>
    /// <returns></returns>
    [HttpPost]
    public Task<OrganizationSimpleDto> AddAsync([FromBody] AddOrganizationCommand command)
    {
        return _organizationService.AddAsync(command);
    }

    [HttpGet("{id}")]
    public Task<OrganizationDetailDto> GetAsync([FromRoute] GetDetailQuery query)
    {
        return _organizationService.GetAsync(query);
    }

    [HttpDelete("{id}")]
    public Task<string> DeleteAsync([FromRoute] DeleteOrganizationCommand command)
    {
        return _organizationService.DeleteAsync(command.Id);
    }

    [HttpPost("{id}")]
    public Task<OrganizationSimpleDto> UpdateAsync([FromRoute, Required, StringLength(36)] string id,
        [FromBody] UpdateOrganizationCommand command)
    {
        command.Id = id;
        return _organizationService.UpdateAsync(command);
    }

    [HttpGet("subList")]
    public Task<List<SubOrganizationDto>> GetSubListAsync([FromQuery] GetSubListQuery query)
    {
        return _organizationService.GetSubListAsync(query);
    }

    [HttpPost("{id}/administrators/{userId}")]
    public Task AddAdministratorAsync([FromRoute] AddAdministratorCommand command)
    {
        return _organizationService.AddAdministratorAsync(command);
    }

    [HttpDelete("{id}/administrators/{userId}")]
    public Task AddAdministratorAsync([FromRoute] DeleteAdministratorCommand command)
    {
        return _organizationService.DeleteAdministratorAsync(command);
    }
}