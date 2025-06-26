using System.Text.Json;
using Microsoft.EntityFrameworkCore;
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
    ILogger<OrganizationService> logger)
    : BaseService(dbContext, session, dbOptions, logger)
{
    public async Task<OrganizationSummaryDto> GetSummaryAsync(GetSummaryQuery query)
    {
        var organization = await DbContext
            .Set<OrganizationDetail>()
            .AsNoTracking()
            .Where(x => x.Id == query.Id)
            .Select(x => new OrganizationSummaryDto
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
        if (Session.IsSupperAdmin())
        {
            return [];
        }

        if (string.IsNullOrEmpty(query.ParentId))
        {
            query.ParentId = null;
        }

        if ("my".Equals(query.Type, StringComparison.OrdinalIgnoreCase))
        {
            return await GetMyListAsync();
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

    // public async Task<bool> ExistsUserAsync(ExistsUserQuery query)
    // {
    //     var organizationUserTable = DbContext.Set<OrganizationUser>();
    //     var organizationTable = DbContext.Set<WildGoose.Domain.Entity.Organization>();
    //
    //     var queryable = from t1 in organizationUserTable
    //         join t2 in organizationTable on t1.OrganizationId equals t2.Id
    //         where t1.UserId == query.UserId && t2.Code == query.Code
    //         select t2.Id;
    //
    //     return await queryable.AnyAsync();
    // }
    //
    // public async Task<bool> IsUserInOrganizationWithInheritanceAsync(
    //     IsUserInOrganizationWithInheritanceQuery query)
    // {
    //     var organizationUserTable = DbContext.Set<OrganizationUser>();
    //     var organizationTable = DbContext.Set<WildGoose.Domain.Entity.Organization>();
    //
    //     var queryable = from t1 in organizationUserTable
    //         join t2 in organizationTable on t1.OrganizationId equals t2.Id
    //         where t1.UserId == query.UserId
    //         select t2.Code;
    //     var organizationList = await queryable.ToListAsync();
    //     return organizationList.Any(x => query.Code.StartsWith(x));
    // }

    private async Task<List<SubOrganizationDto>> GetMyListAsync()
    {
        var myOrganizationsQueryable = DbContext.Set<OrganizationDetail>()
            .Where(x => DbContext.Set<OrganizationUser>()
                .Where(y => y.UserId == Session.UserId)
                .Select(z => z.OrganizationId).Contains(x.Id));

        var organizations = await myOrganizationsQueryable
            .AsNoTracking()
            .OrderBy(x => x.Level)
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
            HasChild = organization.HasChild
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

//         var sql = $$"""
//                     SELECT t1.id,
//                            t1.name,
//                            t1.metadata,
//                            t1.code,
//                            t1.parent_id,
//                            t1.parent_name,
//                            t1.has_child,
//                            t3.scope
//                     FROM {{Defaults.OrganizationDetailTableName}} t1
//                              JOIN {{Defaults.OrganizationUserTableName}} t2 ON t1.id = t2.organization_id
//                              JOIN {{Defaults.OrganizationScopeTableName}} t3 ON t1.id = t3.organization_id
//                     WHERE NOT (t1.is_deleted)
//                       AND t2.user_id = @Id
//                     """;
//         if (DbOptions.EnableSensitiveDataLogging)
//         {
//             Logger.LogInformation(sql);
//         }
//
//         var conn = DbContext.Database.GetDbConnection();
//         var entities = (await
//                 conn.QueryAsync<OrganizationWithScopeEntity>(sql, new { Id = Session.UserId }, commandTimeout: 30))
//             .ToList();
//         if (entities.Count == 0)
//         {
//             return [];
//         }
//
//         var dict = new Dictionary<string, SubOrganizationDto>();
//         foreach (var entity in entities)
//         {
//             SubOrganizationDto dto;
//             if (!dict.TryGetValue(entity.Id, out var value))
//             {
//                 dto = new SubOrganizationDto
//                 {
//                     Id = entity.Id,
//                     Name = entity.Name,
//                     Code = entity.Code,
//                     HasChild = entity.HasChild,
//                     ParentId = entity.ParentId,
//                     ParentName = entity.ParentName,
//                     Metadata = string.IsNullOrEmpty(entity.Metadata) ? null : JsonDocument.Parse(entity.Metadata),
//                     Scope = string.IsNullOrEmpty(entity.Scope)
//                         ? []
//                         : [entity.Scope]
//                 };
//                 dict.Add(entity.Id, dto);
//             }
//             else
//             {
//                 if (string.IsNullOrEmpty(entity.Scope))
//                 {
//                     continue;
//                 }
//
//                 dto = value;
//                 dto.Scope.Add(entity.Scope);
//             }
//         }
//
//         return dict.Values.ToList();
    }

    // class OrganizationWithScopeEntity
    // {
    //     public string Id { get; set; }
    //     public string Name { get; set; }
    //     public string Metadata { get; set; }
    //     public string Scope { get; set; }
    //     public string ParentId { get; set; }
    //     public string ParentName { get; set; }
    //     public bool HasChild { get; set; }
    //     public string Code { get; set; }
    // }
}