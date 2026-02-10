using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WildGoose.Application.Extensions;
using WildGoose.Application.Organization.V10.Dto;
using WildGoose.Application.Organization.V10.Queries;
using WildGoose.Domain;
using WildGoose.Domain.Entity;
using WildGoose.Infrastructure;

namespace WildGoose.Application.Organization.V10;

public class OrganizationService(
    WildGooseDbContext dbContext,
    ISession session,
    IOptions<DbOptions> dbOptions,
    IMemoryCache memoryCache,
    ILogger<OrganizationService> logger)
    : BaseService(dbContext, session, dbOptions, logger, memoryCache)
{
    public async Task<OrganizationDetailDto> GetDetailAsync(GetDetailQuery query)
    {
        var organization = await DbContext
            .Set<OrganizationDetail>()
            .AsNoTracking()
            .Where(x => x.Id == query.Id)
            .Select(x => new OrganizationDetailDto
            {
                Id = x.Id,
                Name = x.Name,
                Code = x.Code,
                ParentId = x.ParentId,
                ParentName = x.ParentName,
                Path = x.Path,
                Branch = x.Branch
            }).FirstOrDefaultAsync();
        return organization;
    }

    /// <summary>
    /// 只返回了机构信息，不含敏感信息，只要登录的就能访问
    /// </summary>
    /// <param name="query"></param>
    /// <returns></returns>
    public async Task<List<SubOrganizationDto>> GetSubListAsync(GetSubListQuery query)
    {
        // 管理与业务分离，禁止管理员访问此接口
        if (Session.IsSupperAdminOrUserAdmin())
        {
            return [];
        }

        if (string.IsNullOrEmpty(query.ParentId))
        {
            query.ParentId = null;

            if ("my".Equals(query.Type, StringComparison.OrdinalIgnoreCase))
            {
                return await GetMyListAsync();
            }
        }

        var result = await DbContext
            .Set<OrganizationDetail>()
            .AsNoTracking()
            .Where(x => x.ParentId == query.ParentId)
            .OrderBy(x => x.Code)
            .Select(organization => new
            {
                organization.Id,
                organization.Name,
                organization.ParentId,
                organization.ParentName,
                organization.Metadata,
                organization.Code,
                Scope = DbContext.Set<OrganizationScope>().AsNoTracking()
                    .Where(y => y.OrganizationId == organization.Id).Select(z => z.Scope).ToList(),
                organization.HasChild
            }).ToListAsync();
        return result.Select(x => new SubOrganizationDto
        {
            Id = x.Id,
            Name = x.Name,
            Code = x.Code,
            ParentId = x.ParentId,
            ParentName = x.ParentName,
            Scope = x.Scope,
            HasChild = x.HasChild,
            Metadata = string.IsNullOrEmpty(x.Metadata) ? null : JsonDocument.Parse(x.Metadata)
        }).ToList();
    }

    private async Task<List<SubOrganizationDto>> GetMyListAsync()
    {
        var queryable = GetUserOrganizationsQueryable(Session.UserId);
        var organizations = await queryable
            .AsNoTracking()
            .OrderBy(x => x.Detail.Level)
            .ThenBy(x => x.Detail.Code)
            .Select(x => x.Detail)
            .ToListAsync();

        if (organizations.Count == 0)
        {
            return new List<SubOrganizationDto>();
        }

        var scopes = await DbContext.Set<OrganizationScope>()
            .AsNoTracking()
            .Where(x => organizations.Select(z => z.Id).Any(y => y == x.OrganizationId))
            .ToListAsync();
        var list = organizations.Select(organization => new SubOrganizationDto
        {
            Id = organization.Id,
            Name = organization.Name,
            Code = organization.Code,
            Path = organization.Path,
            ParentId = organization.ParentId,
            ParentName = organization.ParentName,
            HasChild = organization.HasChild,
            Metadata = string.IsNullOrEmpty(organization.Metadata) ? null : JsonDocument.Parse(organization.Metadata)
        }).ToList();
        var result = new List<SubOrganizationDto>();
        foreach (var dto in list)
        {
            if (result.Any(x => dto.Path.StartsWith(x.Path)))
            {
                continue;
            }

            dto.Scope = scopes.Where(x => x.OrganizationId == dto.Id).Select(x => x.Scope).ToList();
            result.Add(dto);
        }

        return result;
    }
}