using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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

namespace WildGoose.Application.User.Admin.V10;

public class UserService(
    WildGooseDbContext dbContext,
    HttpSession session,
    IOptions<DbOptions> dbOptions,
    ILogger<UserService> logger,
    IPasswordValidator<WildGoose.Domain.Entity.User> passwordValidator,
    UserManager<WildGoose.Domain.Entity.User> userManager,
    IOptions<DaprOptions> dapOptions,
    IOptions<IdentityExtensionOptions> identityExtensionOptions,
    IObjectStorageService objectStorageService)
    : BaseService(dbContext, session, dbOptions, logger)
{
    private static readonly HashSet<string> ImagePostfixes = new() { ".webp", ".bmp", ".jpg", ".jpeg", ".png", ".gif" };
    private readonly IdentityExtensionOptions _identityExtensionOptions = identityExtensionOptions.Value;
    private readonly DaprOptions _daprOptions = dapOptions.Value;

    public async Task<PagedResult<UserDto>> GetAsync(GetUsersQuery query)
    {
        IQueryable<WildGoose.Domain.Entity.User> queryable;
        if (!string.IsNullOrEmpty(query.OrganizationId))
        {
            queryable = from user in DbContext.Set<WildGoose.Domain.Entity.User>()
                join organizationUser in DbContext.Set<OrganizationUser>() on user.Id equals organizationUser.UserId
                where organizationUser.OrganizationId == query.OrganizationId
                select user;
        }
        else
        {
            queryable = DbContext.Set<WildGoose.Domain.Entity.User>()
                    .Where(u => !DbContext.Set<OrganizationUser>().Any(uo => uo.UserId == u.Id))
                ;
        }

        if (!string.IsNullOrEmpty(query.Q))
        {
            queryable = queryable.Where(x =>
                x.UserName.Contains(query.Q) || x.PhoneNumber.Contains(query.Q) || x.Email.Contains(query.Q));
        }

        if ("disabled".Equals(query.Status, StringComparison.OrdinalIgnoreCase))
        {
            queryable = queryable.Where(x => x.LockoutEnabled);
        }

        if ("enabled".Equals(query.Status, StringComparison.OrdinalIgnoreCase))
        {
            queryable = queryable.Where(x => !x.LockoutEnabled);
        }

        var result = await queryable.OrderByDescending(x => x.CreationTime).Select(x => new
        {
            x.Id,
            x.UserName,
            x.PhoneNumber,
            x.Name,
            Enabled = !x.LockoutEnabled,
            IsAdministrator = DbContext.Set<OrganizationAdministrator>()
                .AsNoTracking().Any(y => y.UserId == x.Id && y.OrganizationId == query.OrganizationId),
            x.CreationTime
        }).PagedQueryAsync(query.Page, query.Limit);

        var list = result.Data.ToList();
        var data = list.Select(x => new UserDto
        {
            Id = x.Id,
            UserName = x.UserName,
            Name = x.Name,
            Enabled = x.Enabled,
            PhoneNumber = x.PhoneNumber,
            IsAdministrator = x.IsAdministrator,
            CreationTime = x.CreationTime.HasValue
                ? x.CreationTime.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
                : "-"
        }).ToList();
        var userIds = data.Select(x => x.Id).ToList();
        var organizationUserList = await (from organizationUser in DbContext.Set<OrganizationUser>()
            join organization in DbContext.Set<WildGoose.Domain.Entity.Organization>() on organizationUser
                .OrganizationId equals organization.Id
            where userIds.Contains(organizationUser.UserId)
            select new
            {
                organizationUser.UserId,
                OrganizationName = organization.Name
            }).ToListAsync();
        foreach (var dto in data)
        {
            dto.Organizations = organizationUserList.Where(x => x.UserId == dto.Id)
                .Select(x => x.OrganizationName).ToList();
        }

        var userRoleList = await (from userRole in DbContext.Set<IdentityUserRole<string>>()
            join role in DbContext.Set<WildGoose.Domain.Entity.Role>() on userRole.RoleId equals role.Id
            where userIds.Contains(userRole.UserId)
            select new
            {
                userRole.UserId,
                RoleName = role.Name
            }).ToListAsync();
        foreach (var dto in data)
        {
            dto.Roles = userRoleList.Where(x => x.UserId == dto.Id)
                .Select(x => x.RoleName).ToList();
        }

        return new PagedResult<UserDto>(result.Page, result.Limit, result.Total, data);
    }

    public async Task AddRoleAsync(AddRoleCommand command)
    {
        var user = await DbContext.Set<WildGoose.Domain.Entity.User>()
            .FirstOrDefaultAsync(x => x.Id == command.UserId);
        if (user == null)
        {
            throw new WildGooseFriendlyException(1, "用户不存在");
        }

        await CheckUserPermissionAsync(user.Id);

        var relationship = new IdentityUserRole<string>
        {
            UserId = user.Id,
            RoleId = command.RoleId
        };

        await DbContext.AddAsync(relationship);
        await DbContext.SaveChangesAsync();
    }

    public async Task DeleteRoleAsync(DeleteRoleCommand command)
    {
        var userExist = await DbContext.Set<WildGoose.Domain.Entity.User>()
            .AnyAsync(x => x.Id == command.UserId);
        if (!userExist)
        {
            throw new WildGooseFriendlyException(1, "用户不存在");
        }

        await CheckUserPermissionAsync(command.UserId);

        var relationship = await DbContext.Set<IdentityUserRole<string>>()
            .FirstOrDefaultAsync(x => x.RoleId == command.RoleId && x.UserId == command.UserId);

        if (relationship != null)
        {
            DbContext.Remove(relationship);
            await DbContext.SaveChangesAsync();
        }
    }

    public async Task<UserDto> AddAsync(AddUserCommand command)
    {
        // 验证密码是否符合要求
        var passwordValidatorResult =
            await passwordValidator.ValidateAsync(userManager, new WildGoose.Domain.Entity.User(),
                command.Password);
        passwordValidatorResult.CheckErrors();

        // 查询当前用户管理的所有机构 ID
        var organizationAdministrators = await DbContext.Set<OrganizationAdministrator>()
            .Where(x => x.UserId == Session.UserId)
            .Select(x => x.OrganizationId).ToListAsync();
        
        foreach (var organization in command.Organizations)
        {
            if (!organizationAdministrators.Contains(organization))
            {
                throw new WildGooseFriendlyException(1, "没有权限添加用户到机构");
            }
        }

        if (command.Roles.Contains(Defaults.OrganizationAdminRoleId))
        {
            throw new WildGooseFriendlyException(1, "用户不存在任何机构， 无法授于企业管理员");
        }

        // 校验可授于角色
        await VerifyRolePermissionAsync(command.Roles);

        var normalizedUserName = userManager.NormalizeName(command.UserName);
        if (await userManager.Users.AnyAsync(x => x.NormalizedUserName == normalizedUserName))
        {
            throw new WildGooseFriendlyException(1, "用户名已经存在");
        }

        var user = new WildGoose.Domain.Entity.User
        {
            Id = ObjectId.GenerateNewId().ToString(),
            PhoneNumber = command.PhoneNumber,
            UserName = command.UserName,
            Name = command.Name,
            NormalizedUserName = normalizedUserName
        };

        var organizations = await SetOrganizationsAsync(user.Id, command.Organizations);
        var roles = await SetRolesAsync(user.Id, command.Roles);

        var userExtension = new UserExtension { Id = user.Id };
        Utils.SetPasswordInfo(userExtension, command.Password);
        await DbContext.AddAsync(userExtension);

        var result = await userManager.CreateAsync(user, command.Password);
        // comments by lewis 20231117: _userManager 会自己调用 SaveChanges
        result.CheckErrors();

        var daprClient = GetDaprClient();
        if (daprClient != null && !string.IsNullOrEmpty(_daprOptions.Pubsub))
        {
            await daprClient.PublishEventAsync(_daprOptions.Pubsub, nameof(UserAddedEvent),
                new UserAddedEvent
                {
                    UserId = user.Id
                });
        }

        return new UserDto
        {
            Id = user.Id,
            UserName = user.UserName,
            Name = user.Name,
            Organizations = organizations,
            Enabled = !user.LockoutEnabled,
            PhoneNumber = user.PhoneNumber,
            Roles = roles,
            IsAdministrator = false,
            CreationTime = user.CreationTime.HasValue
                ? user.CreationTime.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
                : "-"
        };
    }

    public async Task DeleteAsync(DeleteUserCommand command)
    {
        var user = await DbContext.Set<WildGoose.Domain.Entity.User>()
            .FirstOrDefaultAsync(x => x.Id == command.Id);
        if (user == null)
        {
            throw new WildGooseFriendlyException(1, "用户不存在");
        }

        await CheckUserPermissionAsync(user.Id);
        DbContext.Remove(user);
        await DbContext.SaveChangesAsync();

        var daprClient = GetDaprClient();
        if (daprClient != null && !string.IsNullOrEmpty(_daprOptions.Pubsub))
        {
            await daprClient.PublishEventAsync(_daprOptions.Pubsub, nameof(UserDeletedEvent),
                new UserDeletedEvent
                {
                    UserId = user.Id
                });
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
        // if (command.Organizations.Length == 0)
        // {
        //     // 只有超管可以添加没有机构的人员
        //     if (!Session.IsSupperAdmin())
        //     {
        //         throw new WildGooseFriendlyException(1, "机构不能为空");
        //     }
        // }

        if (command.Organizations.Length == 0 && command.Roles.Contains(Defaults.OrganizationAdminRoleId))
        {
            throw new WildGooseFriendlyException(1, "用户不存在任何机构， 无法授于企业管理员");
        }

        var normalizedUserName = userManager.NormalizeName(command.UserName);
        if (await DbContext.Set<WildGoose.Domain.Entity.User>()
                .AnyAsync(x => x.Id != command.Id && x.NormalizedUserName == normalizedUserName))
        {
            throw new WildGooseFriendlyException(1, "用户名已经存在");
        }

        var user = await userManager.FindByIdAsync(command.Id);
        if (user == null)
        {
            throw new WildGooseFriendlyException(1, "用户不存在");
        }

        user.Code = command.Code;
        user.Name = command.Name;

        // 优化
        await userManager.SetEmailAsync(user, command.Email);
        await userManager.SetPhoneNumberAsync(user, command.PhoneNumber);
        await userManager.SetUserNameAsync(user, command.UserName);

        // TODO: 每次 SetUserNameAsync SetPhoneNumberAsync 都会调用 UpdateNormalizedUserNameAsync
        await userManager.UpdateNormalizedUserNameAsync(user);


        var organizationIds = await UpdateOrganizationsAsync(user, command.Organizations);
        var roleIds = await UpdateRolesAsync(user, command.Roles);

        var userExtension = await DbContext.Set<UserExtension>()
            .FirstOrDefaultAsync(x => x.Id == command.Id) ?? new UserExtension { Id = user.Id };

        userExtension.Title = command.Title;
        userExtension.DepartureTime = command.DepartureTime;
        userExtension.HiddenSensitiveData = command.HiddenSensitiveData;

        DbContext.Attach(userExtension);

        var organizations = await DbContext.Set<WildGoose.Domain.Entity.Organization>()
            .Where(x => organizationIds.Contains(x.Id))
            .Select(x => x.Name).ToListAsync();
        var roles = await DbContext.Set<WildGoose.Domain.Entity.Role>()
            .Where(x => roleIds.Contains(x.Id))
            .Select(x => x.Name).ToListAsync();

        await DbContext.SaveChangesAsync();

        return new UserDto
        {
            Id = user.Id,
            UserName = user.UserName,
            Name = user.Name,
            Organizations = organizations,
            Enabled = !user.LockoutEnabled,
            PhoneNumber = user.PhoneNumber,
            Roles = roles,
            IsAdministrator = null,
            CreationTime = user.CreationTime.HasValue
                ? user.CreationTime.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
                : "-"
        };

//         await using var transaction = await DbContext.Database.BeginTransactionAsync();
//
//         try
//         {
//             var conn = DbContext.Database.GetDbConnection();
//
//             // // 删除用户的所有机构
//             var t1 = DbContext.Set<OrganizationUser>().EntityType.GetTableName();
//             var t2 = DbContext.Set<IdentityUserRole<string>>().EntityType.GetTableName();
//             var t3 = DbContext.Set<IdentityRole<string>>().EntityType.GetTableName();
//             
//             await conn.ExecuteAsync($$"""
//                                       DELETE FROM {{t1}} WHERE user_id = @UserId;
//                                       DELETE FROM {{t2}} WHERE user_id = @UserId AND role_id
//                                           NOT IN (SELECT id FROM {{t3}} WHERE normalized_name IN ('{{Defaults.AdminRole}}', '{{Defaults.OrganizationAdmin}}'));
//                                       """, new { UserId = user.Id });
//             await SetOrganizationAndRoleAsync(user.Id, command.Organizations, command.Roles);
//
//             await DbContext.SaveChangesAsync();
//             await transaction.CommitAsync();
//         }
//         catch (Exception e)
//         {
//             Logger.LogError("修改用户失败 {Exception}", e);
//             try
//             {
//                 await transaction.RollbackAsync();
//             }
//             catch (Exception e1)
//             {
//                 Logger.LogError("执行回滚失败 {Exception}", e1);
//             }
//         }
    }

    public async Task<UserDetailDto> GetAsync(GetUserDetailQuery query)
    {
        var user = await DbContext.Set<WildGoose.Domain.Entity.User>()
            .AsNoTracking()
            .Where(x => x.Id == query.Id)
            .FirstOrDefaultAsync();
        if (user == null)
        {
            return null;
        }

        var userExtension = await DbContext.Set<UserExtension>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == query.Id);

        var dto = new UserDetailDto
        {
            Id = user.Id,
            Name = user.Name,
            UserName = user.UserName,
            PhoneNumber = user.PhoneNumber,
            Title = userExtension?.Title,
            DepartureTime = userExtension?.DepartureTime?.ToLocalTime().ToUnixTimeSeconds(),
            Email = user.Email,
            Code = user.Code,
            HiddenSensitiveData = userExtension?.HiddenSensitiveData ?? false
        };

        var roles = await (from userRole in DbContext.Set<IdentityUserRole<string>>()
            join role in DbContext.Set<WildGoose.Domain.Entity.Role>() on userRole.RoleId equals role.Id
            where userRole.UserId == query.Id
            select new UserDetailDto.RoleDto
            {
                Id = role.Id,
                Name = role.Name
            }).ToListAsync();
        dto.Roles = roles;

        var organizations = await (from organizationUser in DbContext.Set<OrganizationUser>()
            join organization in DbContext.Set<WildGoose.Domain.Entity.Organization>() on organizationUser
                .OrganizationId equals organization.Id
            where organizationUser.UserId == query.Id
            select new UserDetailDto.OrganizationDto
            {
                Id = organization.Id,
                Name = organization.Name,
                ParentId = organization.Parent.Id,
                HasChild = DbContext
                    .Set<WildGoose.Domain.Entity.Organization>()
                    .Any(x => x.Parent.Id == organization.Id)
            }).ToListAsync();
        dto.Organizations = organizations;
        return dto;
    }

    private async Task<List<string>> UpdateRolesAsync(WildGoose.Domain.Entity.User user, string[] roleIds)
    {
        var userRoles = await DbContext.Set<IdentityUserRole<string>>()
            .AsNoTracking()
            .Where(x => x.UserId == user.Id)
            .ToListAsync();
        var originUserRoleIds = userRoles.Select(x => x.RoleId).ToList();
        // 如果删除了别人授于的角色， 也是允许的。若要加回来， 只能让有权限的人操作。
        var removeIdList = originUserRoleIds.Except(roleIds).ToList();
        removeIdList.Remove(Defaults.OrganizationAdminRoleId);
        // 添加的角色要鉴权
        var addIdList = roleIds.Except(originUserRoleIds).ToList();
        await VerifyRolePermissionAsync(addIdList);

        var removeList =
            userRoles.Where(x => removeIdList.Contains(x.RoleId)).ToList();

        await DbContext.AddRangeAsync(addIdList.Select(x => new IdentityUserRole<string>
        {
            UserId = user.Id,
            RoleId = x
        }));
        DbContext.RemoveRange(removeList);

        foreach (var id in removeIdList)
        {
            originUserRoleIds.Remove(id);
        }

        originUserRoleIds.AddRange(addIdList);
        return originUserRoleIds;
    }

    private async Task<List<string>> UpdateOrganizationsAsync(WildGoose.Domain.Entity.User user, string[] organizations)
    {
        var organizationUsers = await DbContext.Set<OrganizationUser>()
            .AsNoTracking()
            .Where(x => x.UserId == user.Id).ToListAsync();
        var originOrganizationIds = organizationUsers.Select(x => x.OrganizationId).ToList();
        var addIdList = organizations.Except(originOrganizationIds).ToArray();

        // TODO: 
        // 判断当前用户对设置的机构有没有权限
        // await VerifyOrganizationPermissionAsync(addIdList);

        // 如果删除了别人添加的机构， 也是允许的。若要加回来， 只能让有权限的人操作。
        var removeIdList = originOrganizationIds.Except(organizations).ToList();
        var removeList =
            organizationUsers.Where(x => removeIdList.Contains(x.OrganizationId)).ToList();

        await DbContext.AddRangeAsync(addIdList.Select(x => new OrganizationUser
        {
            UserId = user.Id,
            OrganizationId = x
        }));
        DbContext.RemoveRange(removeList);

        foreach (var id in removeIdList)
        {
            originOrganizationIds.Remove(id);
        }

        originOrganizationIds.AddRange(addIdList);
        return originOrganizationIds;
    }

    public async Task ChangePasswordAsync(ChangePasswordCommand command)
    {
        var password = command.ConfirmPassword;
        var passwordValidatorResult =
            await passwordValidator.ValidateAsync(userManager, new WildGoose.Domain.Entity.User(), password);
        passwordValidatorResult.CheckErrors();

        var user = await userManager.FindByIdAsync(command.Id);
        if (user == null)
        {
            throw new WildGooseFriendlyException(1, "用户不存在");
        }

        await CheckUserPermissionAsync(user.Id);

        var extension = await DbContext.Set<UserExtension>()
            .FirstOrDefaultAsync(x => x.Id == user.Id);
        if (extension == null)
        {
            extension = new UserExtension { Id = user.Id };
            Utils.SetPasswordInfo(extension, password);
            await DbContext.AddAsync(extension);
        }
        else
        {
            Utils.SetPasswordInfo(extension, password);
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        (await userManager.ResetPasswordAsync(user, token, password)).CheckErrors();
    }

    public async Task DisableAsync(DisableUserCommand command)
    {
        var user = await DbContext.Set<WildGoose.Domain.Entity.User>()
            .FirstOrDefaultAsync(x => x.Id == command.Id);
        if (user == null)
        {
            throw new WildGooseFriendlyException(1, "用户不存在");
        }

        await CheckUserPermissionAsync(user.Id);

        await userManager.SetLockoutEnabledAsync(user, true);
        await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);

        var daprClient = GetDaprClient();
        if (daprClient != null && !string.IsNullOrEmpty(_daprOptions.Pubsub))
        {
            await daprClient.PublishEventAsync(_daprOptions.Pubsub, nameof(UserDisabledEvent),
                new UserDisabledEvent
                {
                    UserId = user.Id
                });
        }
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

        await userManager.SetLockoutEnabledAsync(user, false);
        await userManager.SetLockoutEndDateAsync(user, null);

        var daprClient = GetDaprClient();
        if (daprClient != null && !string.IsNullOrEmpty(_daprOptions.Pubsub))
        {
            await daprClient.PublishEventAsync(_daprOptions.Pubsub, nameof(UserEnabledEvent),
                new UserEnabledEvent
                {
                    UserId = user.Id
                });
        }
    }

    public async Task SetPictureAsync(SetPictureCommand command)
    {
        var tuple = GetFile();
        var user = await DbContext.Set<WildGoose.Domain.Entity.User>()
            .FirstOrDefaultAsync(x => x.Id == command.Id);
        if (user == null)
        {
            throw new WildGooseFriendlyException(1, "用户不存在");
        }

        await CheckUserPermissionAsync(user.Id);
        // 图片的文件名称会固定，每个用户会不一样，避免产生垃圾文件
        var key = $"/user/picture/{user.Id}{tuple.Type}";

        await using var stream = tuple.File.OpenReadStream();
        var md5 = await CryptographyUtil.ComputeMd5Async(stream);

        var ossResult = await objectStorageService.PutAsync(key, stream);
        if (string.IsNullOrEmpty(ossResult))
        {
            throw new WildGooseFriendlyException(1, "上传头像失败");
        }

        user.Picture = $"{ossResult}?_v={md5}";
        await DbContext.SaveChangesAsync();
    }

    private (IFormFile File, string Type) GetFile()
    {
        if (Session.HttpContext == null)
        {
            throw new WildGooseFriendlyException(1, "HTTP 上下文异常");
        }

        var file = Session.HttpContext.Request.Form.Files.FirstOrDefault();
        if (file == null)
        {
            throw new WildGooseFriendlyException(1, "上传文件异常");
        }

        var fileName = file.FileName;
        // 文件大小控制在1MB， 参考 github 个人信息
        if (file.Length > 1024 * 1024)
        {
            throw new WildGooseFriendlyException(1, "图片需小于 1 MB");
        }

        var type = Path.GetExtension(fileName);
        // 文件后缀控制
        if (string.IsNullOrWhiteSpace(type) || !ImagePostfixes.Contains(type))
        {
            throw new WildGooseFriendlyException(1, $"图片仅支持: {string.Join(',', ImagePostfixes)}");
        }

        return (file, type);
    }

    private async Task<List<string>> SetOrganizationsAsync(string userId,
        string[] organizationIds)
    {
        // 添加成员到机构
        foreach (var organizationId in organizationIds)
        {
            await DbContext.AddAsync(new OrganizationUser
            {
                OrganizationId = organizationId,
                UserId = userId
            });
        }

        return await DbContext.Set<WildGoose.Domain.Entity.Organization>()
            .Where(x => organizationIds.Contains(x.Id))
            .Select(x => x.Name).ToListAsync();
    }

    private async Task<List<string>> SetRolesAsync(string userId,
        List<string> roleIds)
    {
        roleIds.Remove(Defaults.OrganizationAdminRoleId);

        var finalRoleIds = new HashSet<string>(roleIds);

        // 添加成员的默认角色
        if (_identityExtensionOptions.DefaultRoles.Length > 0)
        {
            var normalizedNames = _identityExtensionOptions.DefaultRoles.Select(x => x.ToUpper()).ToArray();
            var defaultRoleIds = await DbContext.Set<WildGoose.Domain.Entity.Role>()
                .AsNoTracking()
                .Where(x => normalizedNames.Contains(x.NormalizedName))
                .Select(x => x.Id)
                .ToListAsync();
            foreach (var roleId in defaultRoleIds)
            {
                finalRoleIds.Add(roleId);
            }
        }

        var finalRoles = new List<IdentityUserRole<string>>();
        foreach (var roleId in finalRoleIds)
        {
            finalRoles.Add(new IdentityUserRole<string>
            {
                RoleId = roleId,
                UserId = userId
            });
        }

        await DbContext.AddRangeAsync(finalRoles);
        var roles = await DbContext.Set<WildGoose.Domain.Entity.Role>()
            .Where(x => finalRoleIds.Contains(x.Id))
            .Select(x => x.Name).ToListAsync();
        return roles;
    }
}