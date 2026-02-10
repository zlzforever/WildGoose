using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using WildGoose.Application.Extensions;
using WildGoose.Application.User.Admin.V10.Command;
using WildGoose.Application.User.Admin.V10.Dto;
using WildGoose.Application.User.Admin.V10.IntegrationEvents;
using WildGoose.Application.User.Admin.V10.Queries;
using WildGoose.Domain;
using WildGoose.Domain.Entity;
using WildGoose.Domain.Extensions;
using WildGoose.Domain.Utils;
using WildGoose.Infrastructure;
using ISession = WildGoose.Domain.ISession;

namespace WildGoose.Application.User.Admin.V10;

public class UserAdminService(
    WildGooseDbContext dbContext,
    ISession session,
    IOptions<DbOptions> dbOptions,
    ILogger<UserAdminService> logger,
    UserManager<WildGoose.Domain.Entity.User> userManager,
    IOptions<DaprOptions> dapOptions,
    IOptions<IdentityExtensionOptions> identityExtensionOptions,
    IMemoryCache memoryCache,
    IObjectStorageService objectStorageService)
    : BaseService(dbContext, session, dbOptions, logger, memoryCache)
{
    private static readonly HashSet<string> ImagePostfixes = [".webp", ".bmp", ".jpg", ".jpeg", ".png", ".gif"];
    private readonly IdentityExtensionOptions _identityExtensionOptions = identityExtensionOptions.Value;
    private readonly DaprOptions _daprOptions = dapOptions.Value;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="query"></param>
    /// <returns></returns>
    public async Task<UserDetailDto> GetAsync(GetUserQuery query)
    {
        var organizations = await GetUserOrganizationsAsync(query.Id);
        await CheckAnyOrganizationPermissionAsync(organizations);

        var userData = await DbContext.Set<WildGoose.Domain.Entity.User>().LeftJoin(
                DbContext.Set<UserExtension>(),
                left => left.Id,
                right => right.Id,
                (left, right) => new
                {
                    User = left,
                    Extension = right
                }
            ).Where(x => x.User.Id == query.Id)
            .FirstOrDefaultAsync();
        if (userData == null)
        {
            return null;
        }

        var dto = new UserDetailDto
        {
            Id = userData.User.Id,
            Name = userData.User.Name,
            UserName = userData.User.UserName,
            PhoneNumber = userData.User.PhoneNumber,
            Email = userData.User.Email,
            Code = userData.User.Code
        };

        if (userData.Extension != null)
        {
            dto.Title = userData.Extension.Title;
            dto.DepartureTime = userData.Extension.DepartureTime?.ToLocalTime().ToUnixTimeSeconds();
            dto.HiddenSensitiveData = userData.Extension.HiddenSensitiveData;
        }

        var roles = await (from userRole in DbContext.Set<IdentityUserRole<string>>()
            join role in DbContext.Set<WildGoose.Domain.Entity.Role>() on userRole.RoleId equals role.Id
            where userRole.UserId == query.Id
            select new UserDetailDto.RoleDto
            {
                Id = role.Id,
                Name = role.Name
            }).ToListAsync();
        dto.Roles = roles;
        dto.Organizations = organizations;
        return dto;
    }

    /// <summary>
    /// 若 organizationId 为空， 组织管理员则查询其有管理权限（含下级）的所有用户， 超级管理员、用户管理员则查询所有用户。
    /// 若 organizationId 不为空， isRecursive 参数才生效， 表示查询的时候是否仅查询本级组织。组织管理员需要检查是否有管理 organizationId 的权限。
    /// </summary>
    /// <param name="query"></param>
    /// <returns></returns>
    public async Task<PagedResult<UserDto>> GetListAsync(GetUserListQuery query)
    {
        if (!string.IsNullOrEmpty(query.OrganizationId))
        {
            return await QueryUsersBySpecificOrganizationAsync(query);
        }

        // 超级管理员、用户管理员查询所有用户
        if (Session.IsSupperAdminOrUserAdmin())
        {
            return await QueryUsersAsync(query.Q, query.Page, query.Limit, query.Status);
        }

        // 组织管理员查询其管理权限下（含下级）的所有用户
        return await QueryUsersByOrganizationAdminAsync(query);
    }

    public async Task<UserDto> AddAsync(AddUserCommand command)
    {
        command.Organizations ??= new();
        command.Roles ??= new();

        if (command.Roles.Contains(Defaults.OrganizationAdminRoleId))
        {
            throw new WildGooseFriendlyException(1, "设置非法角色： 企业管理员");
        }

        // 机构管理员添加用户时，必须设置机构，不然无法鉴权。
        if (Session.IsOrganizationAdmin() && command.Organizations.Count == 0)
        {
            throw new WildGooseFriendlyException(403, "访问受限");
        }

        // 校验可授于角色
        await CheckAllRolePermissionAsync(command.Roles);

        var normalizedUserName = userManager.NormalizeName(command.UserName);
        var user = new WildGoose.Domain.Entity.User
        {
            Id = ObjectId.GenerateNewId().ToString(),
            PhoneNumber = command.PhoneNumber,
            UserName = command.UserName,
            Name = command.Name,
            GivenName = command.Name,
            NormalizedUserName = normalizedUserName
        };

        // 只能设置当前用户可管理的机构
        var organizations = await SetOrganizationsAsync(user.Id, command.Organizations);
        var roles = SetRoles(command.Roles);

        var userExtension = new UserExtension { Id = user.Id };
        Utils.SetPasswordInfo(userExtension, command.Password);
        await DbContext.AddAsync(userExtension);

        // 会做用户名、手机、密码校验（Identity 框架会自动添加默认角色）
        var result = await userManager.CreateAsync(user, command.Password);
        await userManager.AddToRolesAsync(user, roles);
        // comments by lewis 20231117: _userManager 会自己调用 SaveChanges
        result.CheckErrors();
        await DbContext.SaveChangesAsync();

        await PublishEventAsync(_daprOptions, new UserAddedEvent
        {
            UserId = user.Id
        });

        return new UserDto
        {
            Id = user.Id,
            UserName = user.UserName,
            Name = user.Name,
            Organizations = organizations,
            Enabled = user.IsEnabled,
            PhoneNumber = user.PhoneNumber,
            Roles = roles,
            IsAdministrator = false,
            CreationTime = user.CreationTime.HasValue
                ? user.CreationTime.Value.ToLocalTime().ToString(Defaults.SecondTimeFormat)
                : "-"
        };
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="command"></param>
    /// <exception cref="WildGooseFriendlyException"></exception>
    public async Task DeleteAsync(DeleteUserCommand command)
    {
        if (command.Id == Session.UserId)
        {
            throw new WildGooseFriendlyException(1, "禁止删除自己");
        }

        var user = await DbContext.Set<WildGoose.Domain.Entity.User>()
            .FirstOrDefaultAsync(x => x.Id == command.Id);
        if (user == null)
        {
            throw new WildGooseFriendlyException(1, "用户不存在");
        }

        await CheckUserPermissionAsync(user.Id);

        await using var transaction = await DbContext.Database.BeginTransactionAsync();
        try
        {
            // commit by henry at 2025/09/11
            // SetLockoutEndDate 前应该检查用户是否支持设置锁定时长
            if (!user.LockoutEnabled)
            {
                await userManager.SetLockoutEnabledAsync(user, true);
            }

            // commit by henry at 2025/09/11 从先删除再锁定(会报错-该用户不允许锁定) 改成 先锁定再删除
            (await userManager.SetLockoutEndDateAsync(
                user,
                DateTimeOffset.MaxValue)).CheckErrors();

            // 使用原生 EF 删除，软删除
            DbContext.Set<WildGoose.Domain.Entity.User>().Remove(user);

            // 删除用户角色、机构、Claims
            await DbContext.Set<IdentityUserClaim<string>>().Where(x => x.UserId == command.Id)
                .ExecuteDeleteAsync();
            await DbContext.Set<IdentityUserRole<string>>().Where(x => x.UserId == command.Id)
                .ExecuteDeleteAsync();
            await DbContext.Set<UserExtension>().Where(x => x.Id == command.Id)
                .ExecuteDeleteAsync();
            await DbContext.Set<IdentityUserLogin<string>>().Where(x => x.UserId == command.Id)
                .ExecuteDeleteAsync();
            await DbContext.Set<IdentityUserToken<string>>().Where(x => x.UserId == command.Id)
                .ExecuteDeleteAsync();
            await DbContext.Set<OrganizationUser>().Where(x => x.UserId == command.Id)
                .ExecuteDeleteAsync();
            await DbContext.Set<OrganizationAdministrator>().Where(x => x.UserId == command.Id)
                .ExecuteDeleteAsync();

            await DbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            await PublishEventAsync(_daprOptions, new UserDeletedEvent
            {
                UserId = user.Id
            });
        }
        catch (Exception e)
        {
            Logger.LogError(e, "删除用户失败");
            try
            {
                await transaction.RollbackAsync();
            }
            catch (Exception e2)
            {
                Logger.LogError(e2, "删除用户回滚失败");
            }

            throw new WildGooseFriendlyException(1, "删除用户失败");
        }
    }

    /// <summary>
    /// 1. 原来无机构，新增机构：正常判断当前用户有没有对相应机构的管理权限，添加对应机构的默认角色
    /// 2. 原来有机构，删除所有机构：只有超管可以删除所有机构，所有机构下的默认角色全部删除
    /// 3. 原来有机构，删除部分
    /// </summary>
    /// <param name="command"></param>
    /// <exception cref="WildGooseFriendlyException"></exception>
    public async Task<UserDto> UpdateAsync(UpdateUserCommand command)
    {
        command.Organizations ??= new();
        command.Roles ??= new();

        var user = await userManager.FindByIdAsync(command.Id);
        if (user == null)
        {
            throw new WildGooseFriendlyException(1, "用户不存在");
        }

        user.Code = command.Code;
        user.Name = command.Name;
        user.GivenName = command.Name;
        user.PhoneNumber = command.PhoneNumber;
        user.UserName = command.UserName;
        user.Email = command.Email;

        var organizations = await UpdateOrganizationsAsync(user, command.Organizations);
        var roles = await UpdateRolesAsync(user, command.Roles);

        var userExtension = await DbContext.Set<UserExtension>()
            .FirstOrDefaultAsync(x => x.Id == command.Id) ?? new UserExtension { Id = user.Id };
        userExtension.Title = command.Title;
        userExtension.DepartureTime = command.DepartureTime;
        userExtension.HiddenSensitiveData = command.HiddenSensitiveData;
        DbContext.Attach(userExtension);

        // var organizations = await DbContext.Set<WildGoose.Domain.Entity.Organization>()
        //     .Where(x => organizationIds.Contains(x.Id))
        //     .Select(x => x.Name).ToListAsync();

        (await userManager.UpdateAsync(user)).CheckErrors();
        await DbContext.SaveChangesAsync();

        return new UserDto
        {
            Id = user.Id,
            UserName = user.UserName,
            Name = user.Name,
            Organizations = organizations,
            Enabled = user.IsEnabled,
            PhoneNumber = user.PhoneNumber,
            Roles = roles,
            IsAdministrator = null,
            CreationTime = user.CreationTime.HasValue
                ? user.CreationTime.Value.ToLocalTime().ToString(Defaults.SecondTimeFormat)
                : "-"
        };
    }

    public async Task ChangePasswordAsync(ChangePasswordCommand command)
    {
        var password = command.ConfirmPassword;

        // ResetPasswordAsync 内部会校验密码是否符合规则
        // var passwordValidatorResult =
        //     await passwordValidator.ValidateAsync(userManager, new WildGoose.Domain.Entity.User(), password);
        // passwordValidatorResult.CheckErrors();

        var user = await userManager.FindByIdAsync(command.Id);
        if (user == null)
        {
            throw new WildGooseFriendlyException(1, "用户不存在");
        }

        await CheckUserPermissionAsync(user.Id);

        var extension = await DbContext.Set<UserExtension>()
            .FirstOrDefaultAsync(x => x.Id == user.Id) ?? new UserExtension { Id = user.Id };
        Utils.SetPasswordInfo(extension, password);
        DbContext.Attach(extension);

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        (await userManager.ResetPasswordAsync(user, token, password)).CheckErrors();
        await DbContext.SaveChangesAsync();
    }

    /// <summary>
    /// </summary>
    /// <param name="command"></param>
    /// <exception cref="WildGooseFriendlyException"></exception>
    public async Task DisableAsync(DisableUserCommand command)
    {
        if (command.Id == Session.UserId)
        {
            throw new WildGooseFriendlyException(1, "禁止禁用自己");
        }

        var user = await DbContext.Set<WildGoose.Domain.Entity.User>()
            .FirstOrDefaultAsync(x => x.Id == command.Id);
        if (user == null)
        {
            throw new WildGooseFriendlyException(1, "用户不存在");
        }

        await CheckUserPermissionAsync(user.Id);

        if (!user.LockoutEnabled)
        {
            await userManager.SetLockoutEnabledAsync(user, true);
        }

        (await userManager.SetLockoutEndDateAsync(
            user,
            DateTimeOffset.MaxValue.UtcDateTime)).CheckErrors();
        await DbContext.SaveChangesAsync();

        await PublishEventAsync(_daprOptions, new UserDisabledEvent
        {
            UserId = user.Id
        });
    }

    public async Task EnableAsync(EnableUserCommand command)
    {
        var user = await DbContext.Set<WildGoose.Domain.Entity.User>()
            .FirstOrDefaultAsync(x => x.Id == command.Id);
        if (user == null)
        {
            throw new WildGooseFriendlyException(1, "用户不存在");
        }

        await CheckUserPermissionAsync(user.Id);

        if (!user.LockoutEnabled)
        {
            await userManager.SetLockoutEnabledAsync(user, true);
        }

        (await userManager.SetLockoutEndDateAsync(user, null)).CheckErrors();
        await DbContext.SaveChangesAsync();

        await PublishEventAsync(_daprOptions, new UserEnabledEvent
        {
            UserId = user.Id
        });
    }

    public async Task SetPictureAsync(SetPictureCommand command)
    {
        var fileInfo = GetFile();
        var user = await DbContext.Set<WildGoose.Domain.Entity.User>()
            .FirstOrDefaultAsync(x => x.Id == command.Id);
        if (user == null)
        {
            throw new WildGooseFriendlyException(1, "用户不存在");
        }

        await CheckUserPermissionAsync(user.Id);
        // 图片的文件名称会固定，每个用户会不一样，避免产生垃圾文件
        var key = $"/usr/img/{user.Id}{fileInfo.Type}";

        await using var stream = fileInfo.File.OpenReadStream();
        var md5 = await CryptographyUtil.ComputeMd5Async(stream);

        var ossResult = await objectStorageService.PutAsync(key, stream);
        if (string.IsNullOrEmpty(ossResult))
        {
            throw new WildGooseFriendlyException(1, "上传头像失败");
        }

        user.Picture = $"{ossResult}?_v={md5}";
        await DbContext.SaveChangesAsync();
    }

    private async Task<List<string>> UpdateRolesAsync(WildGoose.Domain.Entity.User user, List<string> roles)
    {
        // 当前用户所拥有的所有角色
        var userRoles = await userManager.GetRolesAsync(user);
        // 如果删除了别人授于的角色， 也是允许的。若要加回来， 只能让有权限的人操作。
        var removeList = userRoles.Except(roles).ToList();
        // 企业管理员不能由编辑界面删除
        removeList.Remove(Defaults.OrganizationAdmin);
        // 添加的角色要鉴权, 若有别人添加的角色
        var addList = roles.Except(userRoles).ToList();
        await CheckAllRolePermissionAsync(addList);

        await userManager.RemoveFromRolesAsync(user, removeList);
        await userManager.AddToRolesAsync(user, addList);

        foreach (var id in removeList)
        {
            roles.Remove(id);
        }

        roles.AddRange(addList);
        return roles;
    }

    private async Task<List<string>> UpdateOrganizationsAsync(WildGoose.Domain.Entity.User user,
        List<string> newOrganizationIds)
    {
        // 用户所在的全部机构
        var organizations = await GetUserOrganizationsAsync(user.Id);
        // 新列表排除旧列表，得到新增列表
        var addIdList = newOrganizationIds.Except(organizations.Select(x => x.Id)).ToList();
        var addOrganizations = await GetOrganizationsAsync(addIdList);
        var adminList = await GetAdminOrganizationsAsync(Session.UserId);
        // 检查新增机构，当前用户是否有管理权限
        if (!CanManageAll(adminList, addOrganizations))
        {
            throw new WildGooseFriendlyException(403, "访问受限");
        }

        var organizationNames = addOrganizations.Select(x => x.Name).ToList();
        organizationNames.AddRange(organizations.Select(x => x.Name));

        // 不能删除不在当前用户管理的机构（说明是别人加的）
        // 现在单个用户详情接口，不会返回当前用户管理不到的机构，避免信息泄露
        // 旧列表排除新列表，得到需要删除的列表
        var removeList = new List<OrganizationEntity>();
        foreach (var organization in organizations)
        {
            if (newOrganizationIds.Any(x => x == organization.Id))
            {
            }
            else
            {
                removeList.Add(organization);
            }
        }

        removeList = removeList.GetAvailable(adminList.Select(x => x.Path)).ToList();
        foreach (var organization in removeList)
        {
            organizationNames.Remove(organization.Name);
        }

        var removeOrganizationUser = await DbContext.Set<OrganizationUser>()
            .Where(x => x.UserId == user.Id && removeList.Select(y => y.Id).Contains(x.OrganizationId))
            .ToListAsync();
        DbContext.RemoveRange(removeOrganizationUser);
        await DbContext.AddRangeAsync(addIdList.Select(x => new OrganizationUser
        {
            UserId = user.Id,
            OrganizationId = x
        }));

        // 若用户在删除的机构里当了管理员，需要去除
        // 查询用户为机构管理员的机构
        var organizationAdministrators = await DbContext.Set<OrganizationAdministrator>()
            .Where(x => x.UserId == user.Id).ToListAsync();
        var removeAdministrators = organizationAdministrators
            .Where(x => removeList.Any(y => y.Id == x.OrganizationId))
            .ToList();
        DbContext.RemoveRange(removeAdministrators);

        if (organizationAdministrators.Count == removeAdministrators.Count)
        {
            // 需要删除管理员角色
            var organizationAdminRoles = await DbContext.Set<IdentityUserRole<string>>()
                .Where(x => x.UserId == user.Id && x.RoleId == Defaults.OrganizationAdminRoleId)
                .ToListAsync();
            DbContext.RemoveRange(organizationAdminRoles);
        }

        // foreach (var id in removeIdList)
        // {
        //     organizationIds.Remove(id);
        // }
        //
        // organizationIds.AddRange(addIdList);
        return organizationNames;
    }

    private (IFormFile File, string Type) GetFile()
    {
        var httpSession = Session as HttpSession;
        if (httpSession == null || httpSession.HttpContext == null)
        {
            throw new WildGooseFriendlyException(1, "HTTP 上下文异常");
        }

        var file = httpSession.HttpContext.Request.Form.Files.FirstOrDefault();
        if (file == null)
        {
            throw new WildGooseFriendlyException(1, "上传文件为空");
        }

        var fileName = file.FileName;
        // 文件大小控制在1MB， 参考 github 个人信息
        if (file.Length > 1024 * 1024 * 2)
        {
            throw new WildGooseFriendlyException(1, "图片需小于 2 MB");
        }

        var type = Path.GetExtension(fileName);
        // 文件后缀控制
        if (string.IsNullOrWhiteSpace(type) || !ImagePostfixes.Contains(type))
        {
            throw new WildGooseFriendlyException(1, $"图片仅支持: {string.Join(',', ImagePostfixes)}");
        }

        return (file, type);
    }

    private async Task<List<string>> SetOrganizationsAsync(string userId, List<string> organizationIds)
    {
        // 传入的机构
        var organizations = await GetOrganizationsAsync(organizationIds);
        // 当前用户可管理的机构
        var adminList = await GetAdminOrganizationsAsync(Session.UserId);

        if (!CanManageAll(adminList, organizations))
        {
            throw new WildGooseFriendlyException(403, "访问受限");
        }

        var nameList = new List<string>();
        // 添加成员到机构
        foreach (var organization in organizations)
        {
            await DbContext.AddAsync(new OrganizationUser
            {
                OrganizationId = organization.Id,
                UserId = userId
            });
            nameList.Add(organization.Name);
        }

        return nameList;
    }

    private ICollection<string> SetRoles(List<string> roleNames)
    {
        // 机构管理员只能通过专有接口添加
        roleNames.Remove(Defaults.OrganizationAdmin);

        var finalRoles = new HashSet<string>(roleNames);

        // 添加成员的默认角色
        if (_identityExtensionOptions.DefaultRoles.Length > 0)
        {
            foreach (var role in _identityExtensionOptions.DefaultRoles)
            {
                finalRoles.Add(role.ToUpper());
            }
        }

        return finalRoles;
    }

    private async Task<PagedResult<UserDto>> QueryUsersAsync(string q, int page, int limit, string status)
    {
        if (!Session.IsSupperAdminOrUserAdmin())
        {
            return new PagedResult<UserDto>(1, limit, 0, new List<UserDto>());
        }

        var queryable = DbContext.Set<WildGoose.Domain.Entity.User>().AsNoTracking();
        queryable = ApplySearchFilters(queryable, q, status);

        var result = await queryable.OrderByDescending(x => x.CreationTime)
            .Select(x => new UserEntity
            {
                Id = x.Id,
                UserName = x.UserName,
                PhoneNumber = x.PhoneNumber,
                Name = x.Name,
                LockoutEnd = x.LockoutEnd,
                CreationTime = x.CreationTime
            })
            .PagedQueryAsync(page, limit);

        return await BuildUserDtoResultAsync(result, null);
    }

    /// <summary>
    /// 组织管理员查询其管理权限下（含下级）的所有用户
    /// 使用 Path 前缀匹配，避免先查出所有组织 ID，提升性能
    /// </summary>
    private async Task<PagedResult<UserDto>> QueryUsersByOrganizationAdminAsync(GetUserListQuery query)
    {
        var adminList = await GetAdminOrganizationsAsync(Session.UserId);
        // 若其管理机构数为空，则无任何可查询
        if (adminList.Count == 0)
        {
            return new PagedResult<UserDto>(1, query.Limit, 0, new List<UserDto>());
        }

        // 获取管理员管理的组织 Path 列表（用于前缀匹配）
        var adminPaths = adminList.Select(x => x.Path).ToList();

        // 一次性查询：User JOIN OrganizationUser JOIN OrganizationDetail，通过 Path 前缀匹配过滤
        // 这样可以利用数据库索引，避免先查出所有组织 ID
        var queryable =
            from user in DbContext.Set<WildGoose.Domain.Entity.User>().AsNoTracking()
            join ou in DbContext.Set<OrganizationUser>().AsNoTracking() on user.Id equals ou.UserId
            join org in DbContext.Set<OrganizationDetail>().AsNoTracking() on ou.OrganizationId equals org.Id
            where adminPaths.Any(path => org.Path.StartsWith(path))
            select user;

        // 应用搜索条件
        queryable = ApplySearchFilters(queryable, query.Q, query.Status);

        var result = await queryable
            .Distinct()
            .OrderByDescending(x => x.CreationTime)
            .Select(x => new UserEntity
            {
                Id = x.Id,
                UserName = x.UserName,
                PhoneNumber = x.PhoneNumber,
                Name = x.Name,
                LockoutEnd = x.LockoutEnd,
                CreationTime = x.CreationTime
            })
            .PagedQueryAsync(query.Page, query.Limit);

        return await BuildUserDtoResultAsync(result, query.OrganizationId);
    }

    /// <summary>
    /// 查询指定组织的用户（根据 isRecursive 参数决定是否递归）
    /// 使用 Path 前缀匹配，避免先查出所有组织 ID，提升性能
    /// </summary>
    private async Task<PagedResult<UserDto>> QueryUsersBySpecificOrganizationAsync(GetUserListQuery query)
    {
        var adminList = await GetAdminOrganizationsAsync(Session.UserId);
        var organizations = await GetOrganizationsAsync([query.OrganizationId]);

        // 机构不存在
        if (organizations.Count == 0)
        {
            return new PagedResult<UserDto>(1, query.Limit, 0, new List<UserDto>());
        }

        // 没有机构权限返回空，不报错
        if (!CanManageAll(adminList, organizations))
        {
            return new PagedResult<UserDto>(1, query.Limit, 0, new List<UserDto>());
        }

        // 获取目标组织的 Path（用于前缀匹配）
        var targetOrganizationPath = organizations[0].Path;
        if (string.IsNullOrEmpty(targetOrganizationPath))
        {
            return new PagedResult<UserDto>(1, query.Limit, 0, new List<UserDto>());
        }

        // 使用 Path 前缀匹配一次性查询，避免先查出所有下级组织 ID
        var queryable =
            from user in DbContext.Set<WildGoose.Domain.Entity.User>().AsNoTracking()
            join ou in DbContext.Set<OrganizationUser>().AsNoTracking() on user.Id equals ou.UserId
            join org in DbContext.Set<OrganizationDetail>().AsNoTracking() on ou.OrganizationId equals org.Id
            where query.IsRecursive
                ? org.Path.StartsWith(targetOrganizationPath) // 递归：匹配所有下级
                : org.Id == query.OrganizationId // 非递归：仅本级
            select user;

        // 应用搜索条件
        queryable = ApplySearchFilters(queryable, query.Q, query.Status);

        var result = await queryable
            .Distinct()
            .OrderByDescending(x => x.CreationTime)
            .Select(x => new UserEntity
            {
                Id = x.Id,
                UserName = x.UserName,
                PhoneNumber = x.PhoneNumber,
                Name = x.Name,
                LockoutEnd = x.LockoutEnd,
                CreationTime = x.CreationTime
            })
            .PagedQueryAsync(query.Page, query.Limit);

        return await BuildUserDtoResultAsync(result, query.OrganizationId);
    }

    /// <summary>
    /// 应用搜索和状态过滤条件
    /// </summary>
    private IQueryable<WildGoose.Domain.Entity.User> ApplySearchFilters(
        IQueryable<WildGoose.Domain.Entity.User> queryable, string q, string status)
    {
        if (!string.IsNullOrEmpty(q))
        {
            var likeExp = $"%{q}%";
            // TODO: 需要优化
            queryable = queryable.Where(x =>
                EF.Functions.Like(x.Name, likeExp) || EF.Functions.Like(x.UserName, likeExp) ||
                EF.Functions.Like(x.PhoneNumber, likeExp) || EF.Functions.Like(x.Email, likeExp));
        }

        var now = DateTimeOffset.UtcNow;
        if ("disabled".Equals(status, StringComparison.OrdinalIgnoreCase))
        {
            // LockoutEnd 是解锁时间，解锁时间大于当前时间，表示还在锁定中
            queryable = queryable.Where(x => x.LockoutEnd > now);
        }

        if ("enabled".Equals(status, StringComparison.OrdinalIgnoreCase))
        {
            queryable = queryable.Where(x => x.LockoutEnd == null || x.LockoutEnd < now);
        }

        return queryable;
    }

    /// <summary>
    /// 构建 UserDto 分页结果
    /// </summary>
    private async Task<PagedResult<UserDto>> BuildUserDtoResultAsync(
        PagedResult<UserEntity> pagedResult, string organizationId)
    {
        var list = pagedResult.Data.ToList();
        var userIdList = list.Select(x => x.Id).ToList();

        var data = list.Select(x => new UserDto
        {
            Id = x.Id,
            UserName = x.UserName,
            Name = x.Name,
            Enabled = WildGoose.Domain.Entity.User.CheckEnabled(x.LockoutEnd),
            PhoneNumber = x.PhoneNumber,
            CreationTime = x.CreationTime.HasValue
                ? x.CreationTime.Value.ToLocalTime().ToString(Defaults.SecondTimeFormat)
                : "-"
        }).ToList();

        if (!data.Any())
        {
            return new PagedResult<UserDto>(pagedResult.Page, pagedResult.Limit, pagedResult.Total, data);
        }

        // Split query 防止 JOIN 性能问题
        // 只有前面查到了用户、且传了机构 ID 才需要去查对应机构的管理员
        var organizationAdministrators = !string.IsNullOrEmpty(organizationId)
            ? await DbContext.Set<OrganizationAdministrator>()
                .AsNoTracking()
                .Where(y => userIdList.Contains(y.UserId) && y.OrganizationId == organizationId)
                .ToListAsync()
            : null;

        // 查询用户所在的所有机构
        var organizationGroups = (await DbContext.Set<OrganizationUser>()
            .LeftJoin(DbContext.Set<OrganizationDetail>(),
                left => left.OrganizationId,
                right => right.Id,
                (organizationUser, detail) => new
                {
                    organizationUser.UserId,
                    Detail = detail
                })
            .Where(x => userIdList.Contains(x.UserId) && x.Detail.Id != null)
            .Select(x => new OrganizationUserEntity
            {
                Id = x.Detail.Id,
                Name = x.Detail.Name,
                UserId = x.UserId
            })
            .ToListAsync()).GroupBy(x => x.UserId).ToList();

        // comments: 角色无所谓，管理路线看到所有角色是没关系的，第三方业务应该独立设计业务分类，而不是使用角色
        var userRoleList = (await DbContext.Set<IdentityUserRole<string>>()
                .LeftJoin(DbContext.Set<WildGoose.Domain.Entity.Role>(),
                    left => left.RoleId,
                    right => right.Id,
                    (userRole, role) => new
                    {
                        userRole.UserId,
                        RoleName = role.Name
                    })
                .Where(x => userIdList.Contains(x.UserId) && x.RoleName != null)
                .ToListAsync())
            .GroupBy(x => x.UserId, x => x.RoleName)
            .ToList();

        foreach (var dto in data)
        {
            dto.IsAdministrator = organizationAdministrators?.Any(y => y.UserId == dto.Id);
            dto.Organizations = organizationGroups.FirstOrDefault(x => x.Key == dto.Id)?.Select(x => x.Name).ToList() ??
                                [];
            dto.Roles = userRoleList.FirstOrDefault(x => x.Key == dto.Id) ?? Enumerable.Empty<string>();
        }

        return new PagedResult<UserDto>(pagedResult.Page, pagedResult.Limit, pagedResult.Total, data);
    }

    private class UserEntity
    {
        public string Id { get; set; }
        public string UserName { get; set; }
        public string PhoneNumber { get; set; }
        public string Name { get; set; }
        public DateTimeOffset? LockoutEnd { get; set; }
        public DateTimeOffset? CreationTime { get; set; }
    }

    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
    private class OrganizationUserEntity : IPath
    {
        public string UserId { get; set; }
        public string Id { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }
    }
}