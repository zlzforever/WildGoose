using Dapper;
using Dapr.Client;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using WildGoose.Application.Extensions;
using WildGoose.Domain;
using WildGoose.Domain.Entity;
using WildGoose.Infrastructure;

namespace WildGoose.Application;

public abstract class BaseService
{
    protected static readonly Lazy<string> QueryAdminOrganizationSql = new(() =>
    {
        var sql = $$"""
                    WITH RECURSIVE recursion AS
                                       (SELECT t1.id, t1.name, t1.parent_id, true as leaf
                                        from {{Defaults.OrganizationTableName}} t1
                                        where t1.id in (select distinct organization_id
                                                        from {{Defaults.OrganizationAdministratorTableName}}
                                                        where user_id = @UserId) and t1.is_deleted <> true
                                        UNION ALL
                                        SELECT t2.id, t2.name, t2.parent_id, false
                                        from {{Defaults.OrganizationTableName}} t2,
                                             recursion t3
                                        WHERE t2.id = t3.parent_id)
                    SELECT distinct *
                    FROM recursion order by leaf desc
                    """;
        return sql;
    });

    private static readonly Lazy<string> QuerySingleOrganizationSql = new(() =>
    {
        var sql = $$"""
                    WITH RECURSIVE recursion AS
                                       (SELECT t1.id, t1.name, t1.parent_id, true as leaf
                                        from {{Defaults.OrganizationTableName}} t1
                                        where t1.id = @OrganizationId and t1.is_deleted <> true
                                        UNION ALL
                                        SELECT t2.id, t2.name, t2.parent_id, false
                                        from {{Defaults.OrganizationTableName}} t2,
                                             recursion t3
                                        WHERE t2.id = t3.parent_id)
                    SELECT distinct *
                    FROM recursion order by leaf desc
                    """;
        return sql;
    });

    protected WildGooseDbContext DbContext { init; get; }
    protected HttpSession Session { init; get; }
    protected DbOptions DbOptions { init; get; }
    protected ILogger Logger { init; get; }

    protected DaprClient GetDaprClient() => Session.HttpContext.RequestServices.GetService<DaprClient>();

    protected BaseService(WildGooseDbContext dbContext, HttpSession session, IOptions<DbOptions> dbOptions,
        ILogger logger)
    {
        DbContext = dbContext;
        Session = session;
        Logger = logger;
        DbOptions = dbOptions.Value;
    }

    /// <summary>
    /// 查询当前用户管理的机构
    /// </summary>
    /// <returns></returns>
    protected async ValueTask<List<OrganizationEntity>> GetAdminOrganizationsAsync()
    {
        var sql = QueryAdminOrganizationSql.Value;
        var enumerable = await DbContext.Database.GetDbConnection().QueryAsync<OrganizationEntity>(
            sql, new
            {
                Session.UserId
            });

        return Build(enumerable);
    }

    /// <summary>
    /// 是否拥有管理某个机构的权限
    /// </summary>
    /// <param name="organizationId"></param>
    /// <returns></returns>
    protected async ValueTask<bool> HasOrganizationPermissionAsync(string organizationId)
    {
        if (Session.IsSupperAdmin())
        {
            return true;
        }

        var sql = $"""
                   {QuerySingleOrganizationSql.Value};
                   {QueryAdminOrganizationSql.Value};
                   """;
        var gridReader = await DbContext.Database.GetDbConnection()
            .QueryMultipleAsync(sql, new { Session.UserId, OrganizationId = organizationId });
        var entityEnumerable1 = await gridReader.ReadAsync<OrganizationEntity>();
        var checkOrganization = Build(entityEnumerable1).First();
        var entityEnumerable2 = await gridReader.ReadAsync<OrganizationEntity>();
        var organizations = Build(entityEnumerable2);
        return organizations.Any(x => checkOrganization.Path.StartsWith(x.Path));
    }

    /// <summary>
    /// 是否拥有管理某个用户的权限
    /// TODO: 需要缓存，如果机构太多会导致循环调用数据库
    /// </summary>
    /// <param name="userId"></param>
    /// <exception cref="WildGooseFriendlyException"></exception>
    protected async Task CheckUserPermissionAsync(string userId)
    {
        if (Session.IsSupperAdmin())
        {
            return;
        }

        // 用户所在的所有机构
        var organizationIdList = await DbContext.Set<OrganizationUser>()
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.OrganizationId)
            .ToArrayAsync();

        foreach (var organizationId in organizationIdList)
        {
            if (await HasOrganizationPermissionAsync(organizationId))
            {
                return;
            }
        }

        throw new WildGooseFriendlyException(1, "权限不足");
    }


    /// <summary>
    /// 查询机构，若机构有上级机构，会被忽略
    /// 如，若查出
    /// /A/B/C
    /// /A/B
    /// 因为 /A/B 包含 /A/B/C， 所以 /A/B/C 会成结果数组中删除
    /// </summary>
    /// <returns></returns>
    private List<OrganizationEntity> Build(IEnumerable<OrganizationEntity> enumerable)
    {
        var organizations = new List<OrganizationEntity>();
        var entityDict = new Dictionary<string, OrganizationEntity>();
        foreach (var entity in enumerable)
        {
            if (entity.Leaf)
            {
                organizations.Add(entity);
            }

            entityDict.TryAdd(entity.Id, entity);
        }

        foreach (var kv in entityDict)
        {
            kv.Value.BuildPath(entityDict);
        }

        var result = new List<OrganizationEntity>();
        // 5/2/1
        foreach (var entity in organizations)
        {
            var remove = false;

            // 5/2
            foreach (var organization in organizations)
            {
                if (organization == entity)
                {
                    continue;
                }

                if (!entity.Path.StartsWith(organization.Path))
                {
                    continue;
                }

                remove = true;
                break;
            }

            if (!remove)
            {
                result.Add(entity);
            }
        }

        return result;
    }

    // protected async Task VerifyOrganizationPermissionAsync(params string[] organizationIds)
    // {
    //     // 要么 organizationIds 为空， 即用户无机构， 一般为系统帐户
    //     // 若有设置机构， 则机构标识不能为空， 且机构必须存在
    //     if (organizationIds.Any(string.IsNullOrEmpty) || !organizationIds.Any(x => ObjectId.TryParse(x, out _)))
    //     {
    //         throw new WildGooseFriendlyException(1, "数据校验失败");
    //     }
    //
    //     if (Session.IsSupperAdmin())
    //     {
    //         return;
    //     }
    //
    //     if (organizationIds.Length == 0)
    //     {
    //         throw new WildGooseFriendlyException(1, "权限不足");
    //     }
    //
    //     if (!await DbContext.AllPermissionAsync(Session.UserId, organizationIds))
    //     {
    //         throw new WildGooseFriendlyException(1, "权限不足");
    //     }
    // }

    protected async Task VerifyRolePermissionAsync(List<string> roleIds)
    {
        if (Session.IsSupperAdmin())
        {
            return;
        }

        if (roleIds.Count == 0)
        {
            return;
        }

        if (roleIds.Any(string.IsNullOrEmpty))
        {
            throw new WildGooseFriendlyException(1, "数据校验失败");
        }

        var userRoles = DbContext.Set<IdentityUserRole<string>>();
        var roleAssignableRoles = DbContext.Set<RoleAssignableRole>();

        var queryable = from ur in userRoles
            join rar in roleAssignableRoles on ur.RoleId equals rar.RoleId
            where ur.UserId == Session.UserId && roleIds.Contains(rar.AssignableId)
            select rar.AssignableId;

        var assignableIds = await queryable.AsNoTracking().Distinct().ToListAsync();

        if (roleIds.Any(x => !assignableIds.Contains(x)))
        {
            throw new WildGooseFriendlyException(1, "权限不足");
        }
    }

    protected class OrganizationEntity
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ParentId { get; set; }
        public bool Leaf { get; set; }
        public string Path { get; set; }

        public void BuildPath(Dictionary<string, OrganizationEntity> dict)
        {
            if (string.IsNullOrEmpty(ParentId))
            {
                Path = Id;
                return;
            }

            var parent = dict[ParentId];
            parent.BuildPath(dict);
            Path = $"{parent.Path}/{Id}";
        }
    }
}