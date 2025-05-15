using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using WildGoose.Application.Organization.V10;
using WildGoose.Application.Organization.V10.Dto;
using WildGoose.Application.Organization.V10.Queries;

namespace WildGoose.Controllers.V10;

[ApiController]
[Route("api/v1.0/organizations")]
[Authorize]
public class OrganizationController(OrganizationService organizationService, IMemoryCache memoryCache) : ControllerBase
{
    private static readonly string FilePath =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot/data/organizations.json");

    [HttpGet("base")]
    public IActionResult GetBaseList()
    {
        var etag = HttpContext.Request.Headers.ETag.ToString();
        var tuple = memoryCache.GetOrCreate("organizations_base.json", entry =>
        {
            (string ETag, byte[] Data) result;
            if (System.IO.File.Exists(FilePath))
            {
                var bytes = System.IO.File.ReadAllBytes(FilePath);
                var md5 = Convert.ToHexString(MD5.HashData(bytes));
                result = new ValueTuple<string, byte[]>(md5, bytes);
            }
            else
            {
                result = (string.Empty, Array.Empty<byte>());
            }

            entry.SetValue(result);
            entry.SlidingExpiration = TimeSpan.FromMinutes(1);
            return result;
        });

        if (string.IsNullOrEmpty(etag))
        {
            HttpContext.Response.Headers.ETag = tuple.ETag;
            return new FileContentResult(tuple.Data, "application/json");
        }

        if (etag == tuple.ETag)
        {
            return StatusCode(304);
        }

        HttpContext.Response.Headers.ETag = tuple.ETag;
        return new ContentResult
        {
            Content = "[]",
            ContentType = "application/json",
            StatusCode = 200
        };
    }

    [HttpGet("subList")]
    public Task<List<SubOrganizationDto>> GetSubListAsync([FromQuery] GetSubListQuery query)
    {
        return organizationService.GetSubListAsync(query);
    }

    [HttpGet("{id}/summary")]
    public Task<OrganizationSummaryDto> GetSummaryAsync([FromRoute] GetSummaryQuery query)
    {
        return organizationService.GetSummaryAsync(query);
    }

    // [HttpHead("users/{userId}")]
    // public Task<bool> ExistsUser([FromRoute] ExistsUserQuery query)
    // {
    //     return organizationService.ExistsUserAsync(query);
    // }

    // [HttpGet("contains")]
    // public Task<bool> IsUserInOrganizationWithInheritance(
    //     [FromQuery, StringLength(36), Required]
    //     string userId,
    //     [FromQuery, StringLength(50), Required]
    //     string code,
    //     [FromQuery, StringLength(10), Required]
    //     string type
    // )
    // {
    //     if ("inherit".Equals(type, StringComparison.OrdinalIgnoreCase))
    //     {
    //         return organizationService.IsUserInOrganizationWithInheritanceAsync(
    //             new IsUserInOrganizationWithInheritanceQuery
    //             {
    //                 UserId = userId,
    //                 Code = code
    //             });
    //     }
    //
    //     return organizationService.ExistsUserAsync(new ExistsUserQuery
    //     {
    //         UserId = userId,
    //     });
    // }

    // [HttpGet("my")]
    // public async Task<List<OrganizationDto>> GetMyListAsync()
    // {
    //     return await _organizationService.GetMyListAsync();
    // }
}