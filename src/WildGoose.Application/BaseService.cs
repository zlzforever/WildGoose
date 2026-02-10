using System.Text.Json;
using Dapr.Client;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WildGoose.Application.Extensions;
using WildGoose.Application.User.Admin.V10.Dto;
using WildGoose.Domain;
using WildGoose.Domain.Entity;
using WildGoose.Infrastructure;

namespace WildGoose.Application;

public abstract class BaseService(
    WildGooseDbContext dbContext,
    ISession session,
    IOptions<DbOptions> dbOptions,
    ILogger logger,
    IMemoryCache memoryCache)
{
    protected WildGooseDbContext DbContext { get; } = dbContext;
    protected ISession Session { get; } = session;
    protected DbOptions DbOptions { init; get; } = dbOptions.Value;
    protected ILogger Logger { get; } = logger;
    protected IMemoryCache MemoryCache { get; } = memoryCache;

    protected DaprClient GetDaprClient()
    {
        var httpSession = Session as HttpSession;
        if (httpSession == null || httpSession.HttpContext == null)
        {
            return null;
        }

        return httpSession.HttpContext.RequestServices.GetService<DaprClient>();
    }


    /// <summary>
    /// 当前用户是否拥有管理某个机构的权限
    /// </summary>
    /// <param name="organizationId"></param>
    /// <returns></returns>
    public Task<bool> CanManageOrganizationAsync(string organizationId)
    {
        return CanManageAllOrganizationsAsync([organizationId]);
    }

    /// <summary>
    /// 是否有管理机构的权限
    /// 传入多个，每个都必需要有权限才返回 true
    /// </summary>
    /// <param name="organizationIdList"></param>
    /// <returns></returns>
    protected async Task<bool> CanManageAllOrganizationsAsync(List<string> organizationIdList)
    {
        if (Session.IsSupperAdminOrUserAdmin())
        {
            return true;
        }

        var adminList = await GetAdminOrganizationsAsync(Session.UserId);
        var organizations = await GetOrganizationsAsync(organizationIdList);
        return CanManageAll(adminList, organizations);
    }

    /// <summary>
    /// 是否拥有管理某个用户的权限
    /// TODO: 需要缓存，如果机构太多会导致循环调用数据库
    /// </summary>
    /// <param name="userId"></param>
    /// <exception cref="WildGooseFriendlyException"></exception>
    public async Task CheckUserPermissionAsync(string userId)
    {
        if (Session.IsSupperAdminOrUserAdmin())
        {
            return;
        }

        var adminList = await GetAdminOrganizationsAsync(Session.UserId);
        if (adminList.Count == 0)
        {
            throw new WildGooseFriendlyException(403, "权限不足");
        }

        var userOrganizations = await GetUserOrganizationsAsync(userId);
        CheckAny(adminList, userOrganizations);
    }

    protected async Task CheckAnyOrganizationPermissionAsync(List<OrganizationEntity> organizations)
    {
        if (Session.IsSupperAdminOrUserAdmin())
        {
            return;
        }

        var adminList = await GetAdminOrganizationsAsync(Session.UserId);
        CheckAny(adminList, organizations);
    }

    protected bool CanManageAll<T>(ICollection<T> adminList, ICollection<T> list) where T : IPath
    {
        if (Session.IsSupperAdminOrUserAdmin())
        {
            return true;
        }

        foreach (var organization in list)
        {
            if (adminList.Any(y => organization.Path.StartsWith(y.Path)))
            {
                // 有权限则继续判断下一个机构
            }
            else
            {
                return false;
            }
        }

        return true;
    }

    protected void CheckAny<T>(ICollection<T> adminList, ICollection<T> list) where T : IPath
    {
        if (Session.IsSupperAdminOrUserAdmin())
        {
            return;
        }

        var result = list.Any(x => adminList.Any(y => x.Path.StartsWith(y.Path)));

        if (result)
        {
            return;
        }

        throw new WildGooseFriendlyException(403, "权限不足");
    }

    protected async Task PublishEventAsync<T>(DaprOptions daprOptions, T @event)
    {
        var daprClient = GetDaprClient();
        if (daprClient != null && !string.IsNullOrEmpty(daprOptions.Pubsub))
        {
            var topicName = typeof(T).Name;
            for (var i = 0; i < 3; i++)
            {
                try
                {
                    await daprClient.PublishEventAsync(daprOptions.Pubsub, topicName, @event);
                    // 执行成功退出
                    break;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "推送消息失败 {TopicName} {Event} {Message}", topicName,
                        JsonSerializer.Serialize(@event), ex.Message);
                    await Task.Delay(100);
                }
            }
        }
    }

    // protected async Task<List<string>> GetAssignableRoleListAsync()
    // {
    //     var userRoles = DbContext.Set<IdentityUserRole<string>>();
    //     var roleAssignableRoles = DbContext.Set<RoleAssignableRole>();
    //
    //     var queryable = from userRole in userRoles
    //         join roleAssignableRole in roleAssignableRoles on userRole.RoleId equals roleAssignableRole.RoleId
    //         where userRole.UserId == Session.UserId
    //         select roleAssignableRole.AssignableId;
    //
    //     var assignableIds = await queryable.AsNoTracking().Distinct().ToListAsync();
    //     return assignableIds;
    // }

    protected async Task<List<string>> GetAssignableRoleNamesAsync()
    {
        var userRoles = DbContext.Set<IdentityUserRole<string>>();
        var roleAssignableRoles = DbContext.Set<RoleAssignableRole>();
        var roles = DbContext.Set<Domain.Entity.Role>();
        var queryable = from userRole in userRoles
            join roleAssignableRole in roleAssignableRoles on userRole.RoleId equals roleAssignableRole.RoleId
            join role in roles on userRole.RoleId equals role.Id
            where userRole.UserId == Session.UserId
            select role.NormalizedName;

        var assignableRoles = await queryable.AsNoTracking().Distinct().ToListAsync();
        return assignableRoles;
    }

    protected async Task CheckAllRolePermissionAsync(List<string> roles)
    {
        if (Session.IsSupperAdminOrUserAdmin())
        {
            return;
        }

        if (roles.Count == 0)
        {
            return;
        }

        if (roles.Any(string.IsNullOrEmpty))
        {
            throw new WildGooseFriendlyException(1, "角色名称不能为空");
        }

        var assignableRoles = await GetAssignableRoleNamesAsync();
        // 传入的角色有任意一个不在可授于角色列表中，则异常
        if (roles.Any(x => !assignableRoles.Contains(x)))
        {
            throw new WildGooseFriendlyException(1, "存在不可授于的角色");
        }
    }

    /// <summary>
    /// 查询用户管理的所有机构
    /// </summary>
    /// <returns></returns>
    protected async Task<List<OrganizationEntity>> GetAdminOrganizationsAsync(string userId)
    {
        return await MemoryCache.GetOrCreateAsync($"WILDGOOSE:AdminOrganizationList:{userId}", async entry =>
        {
            var list = await GetAdminOrganizationsQueryable(userId)
                .Select(x => OrganizationEntity.Build(x.Detail))
                .ToListAsync();
            entry.SetValue(list);
            entry.SetSlidingExpiration(TimeSpan.FromMinutes(1));
            return list;
        });
    }

    protected IQueryable<UserOrganizationPair> GetAdminOrganizationsQueryable(string userId)
    {
        // var result = DbContext.Set<OrganizationAdministrator>()
        //     .LeftJoin(DbContext.Set<OrganizationDetail>(), left => left.OrganizationId,
        //         right => right.Id, (administrator, detail) => new UserOrganizationPair
        //         {
        //             UserId = administrator.UserId,
        //             Detail = detail
        //         }).Where(x => x.UserId == userId && x.Detail.Id != null);

        var result = from administrator in DbContext.Set<OrganizationAdministrator>()
            join detail in DbContext.Set<OrganizationDetail>()
                on administrator.OrganizationId equals detail.Id into detailGroup
            from detail in detailGroup.DefaultIfEmpty()
            select new UserOrganizationPair
            {
                UserId = administrator.UserId,
                Detail = detail
            }
            // 筛选：用户ID匹配 + Detail.Id 不为空（排除无匹配的左连接数据）
            into pair
            where pair.UserId == userId && pair.Detail.Id != null
            select pair;
        return result;
    }

    /// <summary>
    /// 查询用户所在机构
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    protected async Task<List<OrganizationEntity>> GetUserOrganizationsAsync(string userId)
    {
        var list = await GetUserOrganizationsQueryable(userId)
            .Select(x => OrganizationEntity.Build(x.Detail))
            .ToListAsync();
        return list;
    }

    protected IQueryable<UserOrganizationPair> GetUserOrganizationsQueryable(string userId)
    {
        // var result = DbContext.Set<OrganizationUser>()
        //     .LeftJoin(DbContext.Set<OrganizationDetail>(), left => left.OrganizationId,
        //         right => right.Id, (administrator, detail) => new UserOrganizationPair
        //         {
        //             UserId = administrator.UserId,
        //             Detail = detail
        //         }).Where(x => x.UserId == userId && x.Detail.Id != null);
        
        var result = from orgUser in DbContext.Set<OrganizationUser>()
            join detail in DbContext.Set<OrganizationDetail>()
                on orgUser.OrganizationId equals detail.Id into detailGroup
            from detail in detailGroup.DefaultIfEmpty()
            select new UserOrganizationPair
            {
                UserId = orgUser.UserId,
                Detail = detail
            }
            into pair
            where pair.UserId == userId && pair.Detail.Id != null
            select pair;
        return result;
    }

    protected async Task<List<OrganizationEntity>> GetOrganizationsAsync(List<string> organizationIdList)
    {
        var list = await DbContext.Set<OrganizationDetail>()
            .Where(x => organizationIdList.Contains(x.Id))
            .Select(x => OrganizationEntity.Build(x))
            .ToListAsync();
        return list;
    }

    protected async Task<OrganizationEntity> GetOrganizationAsync(string organizationId)
    {
        var dto = await DbContext.Set<OrganizationDetail>()
            .Where(x => x.Id == organizationId)
            .Select(x => OrganizationEntity.Build(x))
            .FirstOrDefaultAsync();
        return dto;
    }

    protected class UserOrganizationPair
    {
        public string UserId { get; set; }
        public OrganizationDetail Detail { get; set; }
    }
}