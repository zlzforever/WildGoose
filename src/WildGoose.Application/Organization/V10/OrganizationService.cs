using System.Text.Json;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WildGoose.Application.Organization.V10.Dto;
using WildGoose.Application.Organization.V10.Queries;
using WildGoose.Infrastructure;

namespace WildGoose.Application.Organization.V10;

public class OrganizationService : BaseService
{
    public OrganizationService(WildGooseDbContext dbContext, HttpSession session, IOptions<DbOptions> dbOptions,
        ILogger<OrganizationService> logger) : base(dbContext, session, dbOptions, logger)
    {
    }

    /// <summary>
    /// 只返回了机构信息，不含敏感信息，只要登录的就能访问
    /// </summary>
    /// <param name="query"></param>
    /// <returns></returns>
    public Task<List<SubOrganizationDto>> GetSubListAsync(GetSubListQuery query)
    {
        return DbContext
            .Set<WildGoose.Domain.Entity.Organization>()
            .Include(x => x.Parent)
            .AsNoTracking()
            .Where(x => x.Parent.Id == query.ParentId)
            .OrderBy(x => x.Code)
            .Select(organization => new SubOrganizationDto
            {
                Id = organization.Id,
                Name = organization.Name,
                ParentId = organization.Parent.Id,
                ParentName = organization.Parent.Name,
                Metadata = organization.Metadata,
                HasChild = DbContext
                    .Set<WildGoose.Domain.Entity.Organization>().AsNoTracking()
                    .Any(x => x.Parent.Id == organization.Id)
            }).ToListAsync();
    }

    public async Task<List<OrganizationDto>> GetMyListAsync()
    {
        var organizationTable = DbContext
            .Set<WildGoose.Domain.Entity.Organization>()
            .EntityType.GetTableName();
        var organizationUserTable = DbContext
            .Set<WildGoose.Domain.Entity.OrganizationUser>()
            .EntityType.GetTableName();
        var organizationScopeTableName = DbContext
            .Set<WildGoose.Domain.Entity.OrganizationScope>()
            .EntityType.GetTableName();
        var sql = $$"""
                    SELECT t1.id, t1.name, t1.metadata, t3.scope
                      FROM {{organizationTable}} t1 LEFT JOIN {{organizationUserTable}} t2
                      ON t1.id = t2.organization_id LEFT JOIN {{organizationScopeTableName}} t3
                      ON t1.id = t3.organization_id
                      WHERE NOT(t1.is_deleted) AND t2.user_id = @Id
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
            return new List<OrganizationDto>();
        }

        var dict = new Dictionary<string, OrganizationDto>();
        foreach (var entity in entities)
        {
            OrganizationDto dto;
            if (!dict.ContainsKey(entity.Id))
            {
                dto = new OrganizationDto
                {
                    Id = entity.Id,
                    Name = entity.Name,
                    Metadata = string.IsNullOrEmpty(entity.Metadata) ? default : JsonDocument.Parse(entity.Metadata),
                    Scope = string.IsNullOrEmpty(entity.Scope)
                        ? new List<string>()
                        : new List<string>
                        {
                            entity.Scope
                        }
                };
                dict.Add(entity.Id, dto);
            }
            else
            {
                if (string.IsNullOrEmpty(entity.Scope))
                {
                    continue;
                }

                dto = dict[entity.Id];
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
    }
}