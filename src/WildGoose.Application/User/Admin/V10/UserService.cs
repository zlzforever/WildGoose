using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using WildGoose.Application.Extensions;
using WildGoose.Application.Role.Admin.V10.Dto;
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

public class UserService : BaseService
{
    private static readonly HashSet<string> ImagePostfixes = new() { ".webp", ".bmp", ".jpg", ".jpeg", ".png", ".gif" };
    private readonly IPasswordValidator<WildGoose.Domain.Entity.User> _passwordValidator;
    private readonly UserManager<WildGoose.Domain.Entity.User> _userManager;
    private readonly IdentityExtensionOptions _identityExtensionOptions;
    private readonly IObjectStorageService _objectStorageService;

    public UserService(WildGooseDbContext dbContext, HttpSession session, IOptions<DbOptions> dbOptions,
        ILogger<UserService> logger, IPasswordValidator<WildGoose.Domain.Entity.User> passwordValidator,
        UserManager<WildGoose.Domain.Entity.User> userManager,
        IOptions<IdentityExtensionOptions> identityExtensionOptions, IObjectStorageService objectStorageService) : base(
        dbContext, session, dbOptions, logger)
    {
        _passwordValidator = passwordValidator;
        _userManager = userManager;
        _objectStorageService = objectStorageService;
        _identityExtensionOptions = identityExtensionOptions.Value;
    }

    public async Task<PagedResult<UserDto>> GetAsync(GetUsersQuery query)
    {
        var queryable = from user in DbContext.Set<WildGoose.Domain.Entity.User>()
            join organizationUser in DbContext.Set<OrganizationUser>() on user.Id equals organizationUser.UserId
            where organizationUser.OrganizationId == query.OrganizationId
            select user;
        if (!string.IsNullOrEmpty(query.Q))
        {
            queryable = queryable.Where(x => x.UserName.Contains(query.Q));
        }

        var result = await queryable.OrderByDescending(x => x.CreationTime).Select(x => new UserDto
        {
            Id = x.Id,
            UserName = x.UserName,
            PhoneNumber = x.PhoneNumber,
            Name = x.Name,
            Enabled = !x.LockoutEnabled,
            CreationTime = x.CreationTime.HasValue ? x.CreationTime.Value.ToString("yyyy-MM-dd HH:mm:ss") : "-"
        }).PagedQueryAsync(query.Page, query.Limit);
        var data = result.Data.ToList();
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

        await VerifyUserPermissionAsync(user.Id);

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

        await VerifyUserPermissionAsync(command.UserId);

        var relationship = await DbContext.Set<IdentityUserRole<string>>()
            .FirstOrDefaultAsync(x => x.RoleId == command.RoleId && x.UserId == command.UserId);

        if (relationship != null)
        {
            DbContext.Remove(relationship);
            await DbContext.SaveChangesAsync();
        }
    }

    public async Task<string> AddAsync(AddUserCommand command)
    {
        // 验证密码是否符合要求
        var passwordValidatorResult =
            await _passwordValidator.ValidateAsync(_userManager, new WildGoose.Domain.Entity.User(),
                command.Password);
        passwordValidatorResult.CheckErrors();

        // TODO
        //// 判断当前用户对设置的机构有没有权限
        // await VerifyOrganizationPermissionAsync(command.Organizations);
        await VerifyRolePermissionAsync(command.Roles);

        var user = new WildGoose.Domain.Entity.User
        {
            Id = ObjectId.GenerateNewId().ToString(),
            PhoneNumber = command.PhoneNumber,
            UserName = command.UserName,
            Name = command.Name,
            NormalizedUserName = command.UserName.ToUpperInvariant()
        };

        await SetOrganizationAndRoleAsync(user.Id, command.Organizations, command.Roles);

        var userExtension = new UserExtension { Id = user.Id };
        Utils.SetPasswordInfo(userExtension, command.Password);
        await DbContext.AddAsync(userExtension);

        var result = await _userManager.CreateAsync(user, command.Password);
        // comments by lewis 20231117: _userManager 会自己调用 SaveChanges
        result.CheckErrors();

        var daprClient = GetDaprClient();
        if (daprClient != null)
        {
            await daprClient.PublishEventAsync("pubsub", nameof(UserAddedEvent),
                new UserAddedEvent
                {
                    UserId = user.Id
                });
        }

        return user.Id;
    }

    public async Task DeleteAsync(DeleteUserCommand command)
    {
        var user = await DbContext.Set<WildGoose.Domain.Entity.User>()
            .FirstOrDefaultAsync(x => x.Id == command.Id);
        if (user == null)
        {
            throw new WildGooseFriendlyException(1, "用户不存在");
        }

        await VerifyUserPermissionAsync(user.Id);
        DbContext.Remove(user);
        await DbContext.SaveChangesAsync();

        var daprClient = GetDaprClient();
        if (daprClient != null)
        {
            await daprClient.PublishEventAsync("pubsub", nameof(UserDeletedEvent),
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
    public async Task UpdateAsync(UpdateUserCommand command)
    {
        if (command.Organizations.Length == 0)
        {
            // 只有超管可以添加没有机构的人员
            if (!Session.IsSupperAdmin())
            {
                throw new WildGooseFriendlyException(1, "机构不能为空");
            }
        }

        if (DbContext.Set<WildGoose.Domain.Entity.User>()
            .AnyAsync(x => x.Id != command.Id && x.UserName == command.UserName).Result)
        {
            throw new WildGooseFriendlyException(1, "用户名已经存在");
        }

        var user = await _userManager.FindByIdAsync(command.Id);
        if (user == null)
        {
            throw new WildGooseFriendlyException(1, "用户不存在");
        }

        user.Code = command.Code;
        user.Name = command.Name;
        await _userManager.SetEmailAsync(user, command.Email);
        await _userManager.SetPhoneNumberAsync(user, command.PhoneNumber);
        await _userManager.SetUserNameAsync(user, command.UserName);

        await SetOrganizationsAsync(user.Id, command.Organizations);
        await SetRolesAsync(user, command.Roles);

        var userExtension = await DbContext.Set<UserExtension>()
            .FirstOrDefaultAsync(x => x.Id == command.Id) ?? new UserExtension { Id = user.Id };

        userExtension.Title = command.Title;
        userExtension.DepartureTime = command.DepartureTime;
        userExtension.HiddenSensitiveData = command.HiddenSensitiveData;

        DbContext.Attach(userExtension);

        await DbContext.SaveChangesAsync();

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

        var userExtension = await DbContext.Set<WildGoose.Domain.Entity.UserExtension>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == query.Id);

        var dto = new UserDetailDto
        {
            Id = user.Id,
            Name = user.Name,
            UserName = user.UserName,
            PhoneNumber = user.PhoneNumber,
            Title = userExtension?.Title,
            DepartureTime = userExtension?.DepartureTime?.ToUnixTimeSeconds(),
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

    private async Task SetRolesAsync(WildGoose.Domain.Entity.User user, string[] roleIds)
    {
        var userRoles = await DbContext.Set<IdentityUserRole<string>>()
            .AsNoTracking()
            .Where(x => x.UserId == user.Id)
            .ToListAsync();
        var userRoleIds = userRoles.Select(x => x.RoleId).ToList();
        // 如果删除了别人授于的角色， 也是允许的。若要加回来， 只能让有权限的人操作。
        var removeIdList = userRoleIds.Except(roleIds).ToArray();
        // 添加的角色要鉴权
        var addIdList = roleIds.Except(userRoleIds).ToArray();
        await VerifyRolePermissionAsync(addIdList);

        var removeList =
            userRoles.Where(x => removeIdList.Contains(x.RoleId)).ToList();
        await DbContext.AddRangeAsync(addIdList.Select(x => new IdentityUserRole<string>
        {
            UserId = user.Id,
            RoleId = x
        }));
        DbContext.RemoveRange(removeList);
    }

    private async Task SetOrganizationsAsync(string userId, string[] organizations)
    {
        var organizationUsers = await DbContext.Set<OrganizationUser>()
            .AsNoTracking()
            .Where(x => x.UserId == userId).ToListAsync();
        var organizationIds = organizationUsers.Select(x => x.OrganizationId).ToList();
        var addIdList = organizations.Except(organizationIds).ToArray();

        // TODO: 
        // 判断当前用户对设置的机构有没有权限
        // await VerifyOrganizationPermissionAsync(addIdList);

        // 如果删除了别人添加的机构， 也是允许的。若要加回来， 只能让有权限的人操作。
        var removeIdList = organizationIds.Except(organizations).ToList();
        var removeList =
            organizationUsers.Where(x => removeIdList.Contains(x.OrganizationId)).ToList();

        await DbContext.AddRangeAsync(addIdList.Select(x => new OrganizationUser
        {
            UserId = userId,
            OrganizationId = x
        }));
        DbContext.RemoveRange(removeList);
    }

    public async Task ChangePasswordAsync(ChangePasswordCommand command)
    {
        var password = command.ConfirmPassword;
        var passwordValidatorResult =
            await _passwordValidator.ValidateAsync(_userManager, new WildGoose.Domain.Entity.User(), password);
        passwordValidatorResult.CheckErrors();

        var user = await _userManager.FindByIdAsync(command.Id);
        if (user == null)
        {
            throw new WildGooseFriendlyException(1, "用户不存在");
        }

        await VerifyUserPermissionAsync(user.Id);

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

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        (await _userManager.ResetPasswordAsync(user, token, password)).CheckErrors();
    }

    public async Task DisableAsync(DisableUserCommand command)
    {
        var user = await DbContext.Set<WildGoose.Domain.Entity.User>()
            .FirstOrDefaultAsync(x => x.Id == command.Id);
        if (user == null)
        {
            throw new WildGooseFriendlyException(1, "用户不存在");
        }

        await VerifyUserPermissionAsync(user.Id);

        await _userManager.SetLockoutEnabledAsync(user, true);
        await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);

        var daprClient = GetDaprClient();
        if (daprClient != null)
        {
            await daprClient.PublishEventAsync("pubsub", nameof(UserDisabledEvent),
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

        await VerifyUserPermissionAsync(user.Id);

        await _userManager.SetLockoutEnabledAsync(user, false);
        await _userManager.SetLockoutEndDateAsync(user, null);

        var daprClient = GetDaprClient();
        if (daprClient != null)
        {
            await daprClient.PublishEventAsync("pubsub", nameof(UserEnabledEvent),
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

        await VerifyUserPermissionAsync(user.Id);
        // 图片的文件名称会固定，每个用户会不一样，避免产生垃圾文件
        var key = $"/user/picture/{user.Id}{tuple.Type}";

        await using var stream = tuple.File.OpenReadStream();
        var md5 = await CryptographyUtil.ComputeMd5Async(stream);

        var ossResult = await _objectStorageService.PutAsync(key, stream);
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

    private async Task SetOrganizationAndRoleAsync(string userId, string[] organizations, string[] roles)
    {
        // 添加成员到机构
        foreach (var organizationId in organizations)
        {
            await DbContext.AddAsync(new OrganizationUser
            {
                OrganizationId = organizationId,
                UserId = userId
            });
        }

        var roleIds = new HashSet<string>(roles);

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
                roleIds.Add(roleId);
            }
        }

        // var organizationDefaultRoleIds = await DbContext.Set<OrganizationRole>()
        //     .AsNoTracking()
        //     .Where(x => organizations.Contains(x.OrganizationId))
        //     .Select(x => x.RoleId)
        //     .ToListAsync();
        //
        // foreach (var roleId in organizationDefaultRoleIds)
        // {
        //     roleIds.Add(roleId);
        // }

        foreach (var roleId in roleIds)
        {
            await DbContext.AddAsync(new IdentityUserRole<string>
            {
                RoleId = roleId,
                UserId = userId
            });
        }
    }
}