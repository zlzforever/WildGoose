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

    protected async Task VerifyUserPermissionAsync(string userId)
    {
        if (Session.IsSupperAdmin())
        {
            return;
        }

        // TODO: 优化到一次查询中
        // 用户所有在机构
        var organizationIdList = await DbContext.Set<OrganizationUser>()
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.OrganizationId)
            .ToArrayAsync();

        if (await DbContext.AnyPermissionAsync(Session.UserId, organizationIdList))
        {
            return;
        }

        throw new WildGooseFriendlyException(1, "权限不足");
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
}