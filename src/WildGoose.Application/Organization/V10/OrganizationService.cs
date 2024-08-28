using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WildGoose.Application.Organization.V10.Dto;
using WildGoose.Application.Organization.V10.Queries;
using WildGoose.Domain.Entity;
using WildGoose.Infrastructure;

namespace WildGoose.Application.Organization.V10;

public class OrganizationService(
    WildGooseDbContext dbContext,
    HttpSession session,
    IOptions<DbOptions> dbOptions,
    ILogger<OrganizationService> logger)
    : BaseService(dbContext, session, dbOptions, logger)
{
    public async Task<OrganizationSummaryDto> GetSummaryAsync(GetSummaryQuery query)
    {
        var organization = await DbContext
            .Set<WildGoose.Domain.Entity.Organization>()
            .Include(x => x.Parent)
            .AsNoTracking()
            .Where(x => x.Id == query.Id)
            .Select(x => new OrganizationSummaryDto
            {
                Id = x.Id,
                Name = x.Name,
                Code = x.Code,
                ParentId = x.Parent.Id,
                ParentName = x.Parent.Name
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
        // parentId 为空， 且不是超级管理员， 只能看到自己所在的机构
        if (string.IsNullOrEmpty(query.ParentId)
            && !Session.IsSupperAdmin() && !"all".Equals(query.Type, StringComparison.OrdinalIgnoreCase))
        {
            return await GetMyListAsync();

            // var list = await DbContext
            //     .Set<WildGoose.Domain.Entity.Organization>()
            //     .Include(x => x.Parent)
            //     .AsNoTracking()
            //     .Where(x => x.Parent.Id == query.ParentId)
            //     .OrderBy(x => x.Code)
            //     .Select(organization => new
            //     {
            //         organization.Id,
            //         organization.Name,
            //         ParentId = organization.Parent.Id,
            //         ParentName = organization.Parent.Name,
            //         organization.Metadata,
            //         Scope = DbContext.Set<OrganizationScope>().AsNoTracking()
            //             .Where(y => y.OrganizationId == organization.Id).Select(z => z.Scope).ToList(),
            //         HasChild = DbContext
            //             .Set<WildGoose.Domain.Entity.Organization>().AsNoTracking()
            //             .Any(x => x.Parent.Id == organization.Id)
            //     }).ToListAsync();
            // return list.Select(x => new SubOrganizationDto
            // {
            //     Id = x.Id,
            //     Name = x.Name,
            //     ParentId = x.ParentId,
            //     ParentName = x.ParentName,
            //     Scope = x.Scope,
            //     HasChild = x.HasChild,
            //     Metadata = string.IsNullOrEmpty(x.Metadata) ? default : JsonDocument.Parse(x.Metadata)
            // }).ToList();
        }

        var result = await DbContext
            .Set<WildGoose.Domain.Entity.Organization>()
            .Include(x => x.Parent)
            .AsNoTracking()
            .Where(x => x.Parent.Id == query.ParentId)
            .OrderBy(x => x.Code)
            .Select(organization => new
            {
                organization.Id,
                organization.Name,
                ParentId = organization.Parent.Id,
                ParentName = organization.Parent.Name,
                organization.Metadata,
                organization.Code,
                Scope = DbContext.Set<OrganizationScope>().AsNoTracking()
                    .Where(y => y.OrganizationId == organization.Id).Select(z => z.Scope).ToList(),
                HasChild = DbContext
                    .Set<WildGoose.Domain.Entity.Organization>().AsNoTracking()
                    .Any(x => x.Parent.Id == organization.Id)
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
            Metadata = string.IsNullOrEmpty(x.Metadata) ? default : JsonDocument.Parse(x.Metadata)
        }).ToList();
    }

    public async Task<bool> ExistsUserAsync(ExistsUserQuery query)
    {
        var organizationUserTable = DbContext.Set<OrganizationUser>();
        var organizationTable = DbContext.Set<WildGoose.Domain.Entity.Organization>();

        var queryable = from t1 in organizationUserTable
            join t2 in organizationTable on t1.OrganizationId equals t2.Id
            where t1.UserId == query.UserId
            select t2.Metadata;
        var organizationList =
            (await queryable.ToListAsync()).Select(x => JsonSerializer.Deserialize<MetadataWithCode>(x)).ToList();
        return organizationList.Any(x => query.Code == x.Code);
    }

    public async Task<bool> IsUserInOrganizationWithInheritanceAsync(
        IsUserInOrganizationWithInheritanceQuery query)
    {
        var organizationUserTable = DbContext.Set<OrganizationUser>();
        var organizationTable = DbContext.Set<WildGoose.Domain.Entity.Organization>();

        var queryable = from t1 in organizationUserTable
            join t2 in organizationTable on t1.OrganizationId equals t2.Id
            where t1.UserId == query.UserId
            select t2.Metadata;
        var organizationList =
            (await queryable.ToListAsync()).Select(x => JsonSerializer.Deserialize<MetadataWithCode>(x)).ToList();
        return organizationList.Any(x => query.Code.StartsWith(x.Code));
    }

    private async Task<List<SubOrganizationDto>> GetMyListAsync()
    {
        var organizationTable = DbContext
            .Set<WildGoose.Domain.Entity.Organization>()
            .EntityType.GetTableName();
        var organizationUserTable = DbContext
            .Set<OrganizationUser>()
            .EntityType.GetTableName();
        var organizationScopeTableName = DbContext
            .Set<OrganizationScope>()
            .EntityType.GetTableName();
        var sql = $$"""
                    SELECT t1.id,
                           t1.name,
                           t1.metadata,
                           t1.code,
                           t3.scope,
                           t4.id     as parent_id,
                           t4.name   as parent_name,
                           (SELECT true
                            FROM {{organizationTable}} AS child
                            WHERE child.parent_id = t1.id
                            limit 1) as has_child
                    FROM {{organizationTable}} t1
                             JOIN {{organizationUserTable}} t2 ON t1.id = t2.organization_id
                             LEFT JOIN {{organizationTable}} t4 ON t1.parent_id = t4.id
                             LEFT JOIN {{organizationScopeTableName}} t3 ON t1.id = t3.organization_id
                    WHERE NOT (t1.is_deleted)
                      AND t2.user_id = @Id
                    """;
        if (DbOptions.EnableSensitiveDataLogging)
        {
            Logger.LogInformation(sql);
        }

        var conn = DbContext.Database.GetDbConnection();
        var entities = (await
                conn.QueryAsync<OrganizationWithScopeEntity>(sql, new { Id = Session.UserId }, commandTimeout: 30))
            .ToList();
        if (entities.Count == 0)
        {
            return [];
        }

        var dict = new Dictionary<string, SubOrganizationDto>();
        foreach (var entity in entities)
        {
            SubOrganizationDto dto;
            if (!dict.TryGetValue(entity.Id, out var value))
            {
                dto = new SubOrganizationDto
                {
                    Id = entity.Id,
                    Name = entity.Name,
                    Code = entity.Code,
                    HasChild = entity.HasChild,
                    ParentId = entity.ParentId,
                    ParentName = entity.ParentName,
                    Metadata = string.IsNullOrEmpty(entity.Metadata) ? default : JsonDocument.Parse(entity.Metadata),
                    Scope = string.IsNullOrEmpty(entity.Scope)
                        ? []
                        : [entity.Scope]
                };
                dict.Add(entity.Id, dto);
            }
            else
            {
                if (string.IsNullOrEmpty(entity.Scope))
                {
                    continue;
                }

                dto = value;
                dto.Scope.Add(entity.Scope);
            }
        }

        return dict.Values.ToList();
    }

    class OrganizationWithScopeEntity
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Metadata { get; set; }
        public string Scope { get; set; }
        public string ParentId { get; set; }
        public string ParentName { get; set; }
        public bool HasChild { get; set; }
        public string Code { get; set; }
    }

    class MetadataWithCode
    {
        /// <summary>
        /// 
        /// </summary>
        [JsonPropertyName("code")]
        public string Code { get; set; }
    }
}