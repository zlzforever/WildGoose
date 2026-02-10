using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using WildGoose.Application.Organization.Admin.V10;
using WildGoose.Application.Organization.Admin.V10.Command;
using WildGoose.Application.Organization.Admin.V10.Dto;
using WildGoose.Application.Organization.Admin.V10.Queries;
using WildGoose.Domain;

namespace WildGoose.Controllers.Admin.V10;

[ApiController]
[Route("api/admin/v1.0/organizations")]
[Microsoft.AspNetCore.Authorization.Authorize(Policy = Defaults.SuperOrUserAdminOrOrgAdminPolicy)]
public class OrganizationController(OrganizationAdminService organizationAdminService) : ControllerBase
{
    /// <summary>
    /// 1. 若 ParentId 为空，admin 则查询顶级机构；organization-admin 则查询他查管理的最顶级机构
    /// </summary>
    /// <param name="query"></param>
    /// <returns></returns>
    [HttpGet("subList")]
    public Task<List<SubOrganizationDto>> GetSubList([FromQuery] GetSubListQuery query)
    {
        return organizationAdminService.GetSubListAsync(query);
    }

    /// <summary>
    /// 根据关键词查询组织机构信息
    /// </summary>
    /// <param name="query"></param>
    /// <returns></returns>
    [HttpGet("search")]
    public Task<List<SearchOrganizationResultItemDto>> Search([FromQuery] SearchQuery query)
    {
        return organizationAdminService.SearchAsync(query);
    }

    /// <summary>
    /// 添加机构
    /// </summary>
    /// <returns></returns>
    [HttpPost]
    public Task<OrganizationSimpleDto> Add([FromBody] AddOrganizationCommand command)
    {
        return organizationAdminService.AddAsync(command);
    }

    [HttpGet("{id}")]
    public Task<OrganizationDetailDto> Get([FromRoute] GetDetailQuery query)
    {
        return organizationAdminService.GetAsync(query);
    }

    [HttpPost("{id}/delete")]
    public Task<string> Delete([FromRoute] DeleteOrganizationCommand command)
    {
        return organizationAdminService.DeleteAsync(command.Id);
    }

    [HttpPost("{id}")]
    public Task<OrganizationSimpleDto> Update([FromRoute, Required, StringLength(36)] string id,
        [FromBody] UpdateOrganizationCommand command)
    {
        command.Id = id;
        return organizationAdminService.UpdateAsync(command);
    }

    [HttpPost("{id}/administrators/{userId}")]
    public Task AddAdministrator([FromRoute] AddAdministratorCommand command)
    {
        return organizationAdminService.AddAdministratorAsync(command);
    }

    [HttpPost("{id}/administrators/{userId}/delete")]
    public Task DeleteAdministrator([FromRoute] DeleteAdministratorCommand command)
    {
        return organizationAdminService.DeleteAdministratorAsync(command);
    }
}