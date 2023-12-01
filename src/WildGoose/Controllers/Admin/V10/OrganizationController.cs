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
[Authorize(Roles = "admin, organization-admin")]
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
    public async Task<OrganizationDetailDto> AddAsync([FromBody] AddOrganizationCommand command)
    {
        return await _organizationService.AddAsync(command);
    }

    [HttpGet("{id}")]
    public async Task<OrganizationDetailDto> GetAsync([FromRoute] GetDetailQuery query)
    {
        return await _organizationService.GetAsync(query);
    }

    [HttpDelete("{id}")]
    public async Task<string> DeleteAsync([FromRoute] DeleteOrganizationCommand command)
    {
        return await _organizationService.DeleteAsync(command.Id);
    }

    [HttpPost("{id}")]
    public async Task<OrganizationDetailDto> UpdateAsync([FromRoute, Required, StringLength(36)] string id,
        [FromBody] UpdateOrganizationCommand command)
    {
        command.Id = id;
        return await _organizationService.UpdateAsync(command);
    }

    [HttpGet("subList")]
    public async Task<List<SubOrganizationDto>> GetSubListAsync([FromQuery] GetSubListQuery query)
    {
        return await _organizationService.GetSubListAsync(query);
    }
}