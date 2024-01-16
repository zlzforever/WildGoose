using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WildGoose.Application.Organization.V10;
using WildGoose.Application.Organization.V10.Dto;
using WildGoose.Application.Organization.V10.Queries;

namespace WildGoose.Controllers.V10;

[ApiController]
[Route("api/v1.0/organizations")]
[Authorize]
public class OrganizationController
{
    private readonly OrganizationService _organizationService;

    public OrganizationController(OrganizationService organizationService)
    {
        _organizationService = organizationService;
    }

    [HttpGet("subList")]
    public Task<List<SubOrganizationDto>> GetSubListAsync([FromQuery] GetSubListQuery query)
    {
        return _organizationService.GetSubListAsync(query);
    }

    [HttpGet("{id}/summary")]
    public Task<OrganizationSummaryDto> GetSummaryAsync([FromRoute] GetSummaryQuery query)
    {
        return _organizationService.GetSummaryAsync(query);
    }

    // [HttpGet("my")]
    // public async Task<List<OrganizationDto>> GetMyListAsync()
    // {
    //     return await _organizationService.GetMyListAsync();
    // }
}