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
public class OrganizationController(OrganizationAdminService organizationAdminService) : ControllerBase
{
    /// <summary>
    /// 1. 若 ParentId 为空，admin 则查询顶级机构；organization-admin 则查询他查管理的最顶级机构
    /// </summary>
    /// <param name="query"></param>
    /// <returns></returns>
    [HttpGet("subList")]
    public Task<List<SubOrganizationDto>> GetSubListAsync([FromQuery] GetSubListQuery query)
    {
        return organizationAdminService.GetSubListAsync(query);
    }

    /// <summary>
    /// 根据关键词查询组织机构信息
    /// </summary>
    /// <param name="query"></param>
    /// <returns></returns>
    [HttpGet("search")]
    public Task<List<OrganizationPathDto>> SearchAsync([FromQuery] GetPathQuery query)
    {
        return organizationAdminService.SearchAsync(query);
    }

    /// <summary>
    /// 添加机构
    /// </summary>
    /// <returns></returns>
    [HttpPost]
    public Task<OrganizationSimpleDto> AddAsync([FromBody] AddOrganizationCommand command)
    {
        return organizationAdminService.AddAsync(command);
    }

    [HttpGet("{id}")]
    public Task<OrganizationDetailDto> GetAsync([FromRoute] GetDetailQuery query)
    {
        return organizationAdminService.GetAsync(query);
    }

    [HttpDelete("{id}")]
    public Task<string> DeleteAsync([FromRoute] DeleteOrganizationCommand command)
    {
        return organizationAdminService.DeleteAsync(command.Id);
    }

    [HttpPost("{id}")]
    public Task<OrganizationSimpleDto> UpdateAsync([FromRoute, Required, StringLength(36)] string id,
        [FromBody] UpdateOrganizationCommand command)
    {
        command.Id = id;
        return organizationAdminService.UpdateAsync(command);
    }

    [HttpPost("{id}/administrators/{userId}")]
    public Task AddAdministratorAsync([FromRoute] AddAdministratorCommand command)
    {
        return organizationAdminService.AddAdministratorAsync(command);
    }

    [HttpDelete("{id}/administrators/{userId}")]
    public Task AddAdministratorAsync([FromRoute] DeleteAdministratorCommand command)
    {
        return organizationAdminService.DeleteAdministratorAsync(command);
    }
}