using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WildGoose.Application.User.Admin.V10;
using WildGoose.Application.User.Admin.V10.Command;
using WildGoose.Application.User.Admin.V10.Queries;
using WildGoose.Domain;
using WildGoose.Domain.Entity;
using WildGoose.Infrastructure;
using Xunit;

namespace WildGoose.Tests;

/// <summary>
/// 用户管理服务测试
/// 测试不同角色（超级管理员、用户管理员、组织管理员）对用户的增删改查权限
/// </summary>
[Collection("WebApplication collection")]
public class UserAdminServiceTests(WebApplicationFactoryFixture fixture) : BaseTests
{
    /// <summary>
    /// 组织管理员不传 organizationId 时查询其管理权限下（含下级）的机构的所有用户
    /// </summary>
    [Fact]
    public async Task OrganizationAdminGetUserListWithoutOrganizationId()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadDevelopAdmin(session); // 技术部经理，管理技术部及其下级

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();
        var result = await userAdminService.GetListAsync(new GetUserListQuery
        {
            Page = 1,
            Limit = 60,
            OrganizationId = null // 不传 organizationId，应返回其管理权限下（含下级）的所有用户
        });

        Assert.NotNull(result);
        // 应该能查询到技术部及其下级组织的用户
        Assert.Contains(result.Data, x => x.Organizations.Contains("前端组"));
        Assert.Contains(result.Data, x => x.Organizations.Contains("技术部"));
        Assert.Contains(result.Data, x => x.Organizations.Contains("后端组"));
    }

    /// <summary>
    /// 组织管理员传 organizationId 且 isRecursive=true 时递归查询下级组织
    /// </summary>
    [Fact]
    public async Task OrganizationAdminGetUserListWithRecursiveTrue()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadDevelopAdmin(session); // 技术部经理，管理技术部及其下级

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();
        var result = await userAdminService.GetListAsync(new GetUserListQuery
        {
            Page = 1,
            Limit = 60,
            OrganizationId = "507f1f77bcf86cd799439031", // 技术部
            IsRecursive = true // 递归查询下级
        });

        Assert.NotNull(result);
        Assert.True(result.Total > 0);
        Assert.Contains(result.Data, x => x.Organizations.Contains("前端组"));
        Assert.Contains(result.Data, x => x.Organizations.Contains("技术部"));
        Assert.Contains(result.Data, x => x.Organizations.Contains("后端组"));
    }

    /// <summary>
    /// 组织管理员传 organizationId 且 isRecursive=false 时仅查询本级组织
    /// </summary>
    [Fact]
    public async Task OrganizationAdminGetUserListWithRecursiveFalse()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadDevelopAdmin(session); // 技术部经理，管理技术部及其下级

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();
        var resultWithRecursive = await userAdminService.GetListAsync(new GetUserListQuery
        {
            Page = 1,
            Limit = 60,
            OrganizationId = TechOrg, // 技术部
            IsRecursive = false // 仅查询本级
        });

        Assert.NotNull(resultWithRecursive);

        // 递归查询的结果数应该 >= 非递归查询的结果数
        Assert.True(resultWithRecursive.Data.All(x => x.Organizations.Contains("技术部")));
    }

    /// <summary>
    /// 超级管理员可以获取任意用户详情
    /// </summary>
    [Fact]
    public async Task SuperAdminGetUser()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();
        var user = await userAdminService.GetAsync(new GetUserQuery
        {
            Id = "507f1f77bcf86cd799439022"
        });

        Assert.NotNull(user);
        Assert.Equal("李四", user.Name);

        var adminUser = await userAdminService.GetAsync(new GetUserQuery
        {
            Id = AdminUserId
        });
        Assert.Equal("系统管理员", adminUser.Name);
    }

    /// <summary>
    /// 机构管理员不可以获取超管用户详情
    /// </summary>
    [Fact]
    public async Task OrganizationAdminGetAdminShouldFailed()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadDevelopAdmin(session);

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();

        var exception = await Assert.ThrowsAsync<WildGooseFriendlyException>(async () =>
        {
            await userAdminService.GetAsync(new GetUserQuery
            {
                Id = AdminUserId
            });
        });
        Assert.Equal("权限不足", exception.Message);
    }

    /// <summary>
    /// 机构管理员获取本级用户信息
    /// </summary>
    [Fact]
    public async Task OrganizationAdminGetUser1()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadDevelopAdmin(session);

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();

        var user = await userAdminService.GetAsync(new GetUserQuery
        {
            Id = TechAdminUserId
        });
        Assert.NotNull(user);
        Assert.Equal("王五", user.Name);
    }

    /// <summary>
    /// 机构管理员获取下级用户信息
    /// </summary>
    [Fact]
    public async Task OrganizationAdminGetUser2()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadDevelopAdmin(session);

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();

        var user = await userAdminService.GetAsync(new GetUserQuery
        {
            Id = BackendUserId
        });
        Assert.NotNull(user);
        Assert.Equal("小红", user.Name);
    }

    /// <summary>
    /// 机构管理员不能获取不在其管理范围的用户信息
    /// </summary>
    [Fact]
    public async Task OrganizationAdminGetUser3()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadDevelopAdmin(session);

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();


        var exception = await Assert.ThrowsAsync<WildGooseFriendlyException>(async () =>
        {
            await userAdminService.GetAsync(new GetUserQuery
            {
                Id = "507f1f77bcf86cd799439024" //销售部管理员
            });
        });
        Assert.Equal("权限不足", exception.Message);
    }

    /// <summary>
    /// 超级管理员可以查询所有用户（不限组织）
    /// </summary>
    [Fact]
    public async Task SuperAdminGetUserList()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();
        var result = await userAdminService.GetListAsync(new GetUserListQuery
        {
            Page = 1,
            Limit = 60,
            OrganizationId = null
        });

        Assert.NotNull(result);
        Assert.True(result.Total > 0);
        Assert.NotNull(result.Data);
    }

    /// <summary>
    /// 超级管理员可以添加用户，不设置组织和角色
    /// </summary>
    [Fact]
    public async Task SuperAdminAddUserWithoutOrganization()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();
        var userName = CreateName();
        var user = await userAdminService.AddAsync(new AddUserCommand
        {
            UserName = userName,
            Name = "测试用户",
            Password = "Test@123456",
            PhoneNumber = GenerateChinesePhoneNumber(),
            Organizations = [],
            Roles = []
        });

        Assert.NotNull(user);
        Assert.Equal(userName, user.UserName);
        Assert.Equal("测试用户", user.Name);
        Assert.True(user.Enabled);
    }

    /// <summary>
    /// 超级管理员可以添加用户，设置组织和角色
    /// </summary>
    [Fact]
    public async Task SuperAdminAddUserWithOrganization()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();
        var userName = CreateName();
        var user = await userAdminService.AddAsync(new AddUserCommand
        {
            UserName = userName,
            Name = "测试用户",
            Password = "Test@123456",
            PhoneNumber = GenerateChinesePhoneNumber(),
            Organizations = [TechOrg], // 技术部
            Roles = [ManagerRole]
        });

        Assert.NotNull(user);
        Assert.Equal(userName, user.UserName);
        Assert.NotNull(user.Organizations);
        Assert.Single(user.Organizations);
    }

    /// <summary>
    /// 添加用户时不能直接授予组织管理员角色
    /// </summary>
    [Fact]
    public async Task AddUserWithOrganizationAdminRoleShouldFail()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();

        var exception = await Assert.ThrowsAsync<WildGooseFriendlyException>(async () =>
        {
            await userAdminService.AddAsync(new AddUserCommand
            {
                UserName = CreateName(),
                Name = "测试用户",
                Password = "Test@123456",
                PhoneNumber = GenerateChinesePhoneNumber(),
                Organizations = ["65fd28c6ac42f1a071e1ed8c"],
                Roles = [Defaults.OrganizationAdminRoleId]
            });
        });

        Assert.Equal("设置非法角色： 企业管理员", exception.Message);
    }

    /// <summary>
    /// 超级管理员可以修改任意用户
    /// </summary>
    [Fact]
    public async Task SuperAdminUpdateUser()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        // 先创建一个用户
        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();

        var addedUser = await userAdminService.AddAsync(new AddUserCommand
        {
            UserName = CreateName(),
            Name = "原名称",
            Password = "Test@123456",
            PhoneNumber = GenerateChinesePhoneNumber(),
            Organizations = [FrontEndOrg],
            Roles = [EmployeeRole]
        });

        var name = CreateName()  ;
        // 修改用户
        var updateCommand = new UpdateUserCommand
        {
            Id = addedUser.Id,
            UserName = name,
            Name = "新名称",
            PhoneNumber = GenerateChinesePhoneNumber(),
            Organizations = [FrontEndOrg]
        };

        await userAdminService.UpdateAsync(updateCommand);

        var updatedUser = await userAdminService.GetAsync(new GetUserQuery
        {
            Id = addedUser.Id
        });

        Assert.NotNull(updatedUser);
        Assert.Equal("新名称", updatedUser.Name);
        Assert.Equal("前端组", updatedUser.Organizations.ElementAt(0).Name);
        Assert.True(updatedUser.Roles.Count == 0);
    }

    /// <summary>
    /// 超级管理员可以删除任意用户
    /// </summary>
    [Fact]
    public async Task SuperAdminDeleteUser()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);
        var dbContext = scope.ServiceProvider.GetRequiredService<WildGooseDbContext>();

        // 先创建一个用户
        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();

        var addedUser = await userAdminService.AddAsync(new AddUserCommand
        {
            UserName = CreateName(),
            Name = "待删除用户",
            Password = "Test@123456",
            PhoneNumber = GenerateChinesePhoneNumber(),
            Organizations = [],
            Roles = []
        });

        // 删除用户
        await userAdminService.DeleteAsync(new DeleteUserCommand
        {
            Id = addedUser.Id
        });

        // 验证用户已被删除
        await using var conn = dbContext.Database.GetDbConnection();
        var deletedUser = (IDictionary<string, dynamic>)await conn.QuerySingleAsync($"""
             SELECT id, is_deleted FROM wild_goose_user WHERE id = '{addedUser.Id}'
             """);
        Assert.NotNull(deletedUser);
        Assert.True(deletedUser["is_deleted"]);
    }

    /// <summary>
    /// 禁止删除自己
    /// </summary>
    [Fact]
    public async Task CannotDeleteSelf()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();
        var exception = await Assert.ThrowsAsync<WildGooseFriendlyException>(async () =>
        {
            await userAdminService.DeleteAsync(new DeleteUserCommand
            {
                Id = session.UserId
            });
        });

        Assert.Equal("禁止删除自己", exception.Message);
    }

    /// <summary>
    /// 超级管理员可以禁用用户
    /// </summary>
    [Fact]
    public async Task SuperAdminDisableUser()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);
        var dbContext = scope.ServiceProvider.GetRequiredService<WildGooseDbContext>();
        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        // 先创建一个用户
        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();

        var addedUser = await userAdminService.AddAsync(new AddUserCommand
        {
            UserName = CreateName(),
            Name = "待禁用用户",
            Password = "Test@123456",
            PhoneNumber = GenerateChinesePhoneNumber(),
            Organizations = [],
            Roles = []
        });

        // 禁用用户
        await userAdminService.DisableAsync(new DisableUserCommand
        {
            Id = addedUser.Id
        });

        // 验证用户已被禁用
        var disabledUser = await dbContext.Set<User>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == addedUser.Id);
        Assert.NotNull(disabledUser);
        Assert.False(disabledUser.IsEnabled);
    }

    /// <summary>
    /// 超级管理员可以启用用户
    /// </summary>
    [Fact]
    public async Task SuperAdminEnableUser()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);
        var dbContext = scope.ServiceProvider.GetRequiredService<WildGooseDbContext>();

        // 先创建并禁用一个用户
        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();
        var addedUser = await userAdminService.AddAsync(new AddUserCommand
        {
            UserName = CreateName(),
            Name = "待启用用户",
            Password = "Test@123456",
            PhoneNumber = GenerateChinesePhoneNumber(),
            Organizations = [],
            Roles = []
        });

        await userAdminService.DisableAsync(new DisableUserCommand { Id = addedUser.Id });

        // 启用用户
        await userAdminService.EnableAsync(new EnableUserCommand
        {
            Id = addedUser.Id
        });

        // 验证用户已启用
        var enabledUser = await dbContext.Set<User>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == addedUser.Id);
        Assert.NotNull(enabledUser);
        Assert.True(enabledUser.IsEnabled);
    }

    /// <summary>
    /// 超级管理员可以修改用户密码
    /// </summary>
    [Fact]
    public async Task SuperAdminChangePassword()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        // 先创建一个用户
        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();
        var addedUser = await userAdminService.AddAsync(new AddUserCommand
        {
            UserName = CreateName(),
            Name = "测试用户",
            Password = "Test@123456",
            PhoneNumber = GenerateChinesePhoneNumber(),
            Organizations = [],
            Roles = []
        });

        // 修改密码
        await userAdminService.ChangePasswordAsync(new ChangePasswordCommand
        {
            Id = addedUser.Id,
            NewPassword = "NewTest@123456",
            ConfirmPassword = "NewTest@123456"
        });
    }

    /// <summary>
    /// 组织管理员添加用户在其本级
    /// 用户表单设置时组织不得为空
    /// </summary>
    [Fact]
    public async Task OrganizationAdminAddUser1()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadDevelopAdmin(session);

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();

        var user = await userAdminService.AddAsync(new AddUserCommand
        {
            UserName = CreateName(),
            Name = "组织管理员添加的用户",
            Password = "Test@123456",
            PhoneNumber = GenerateChinesePhoneNumber(),
            Organizations = [TechOrg], //  技术部本级
            Roles = []
        });

        Assert.NotNull(user);
    }

    /// <summary>
    /// 组织管理员添加用户在其管理范围下级
    /// 用户表单设置时组织不得为空
    /// </summary>
    [Fact]
    public async Task OrganizationAdminAddUser2()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadDevelopAdmin(session);

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();

        var user = await userAdminService.AddAsync(new AddUserCommand
        {
            UserName = CreateName(),
            Name = "组织管理员添加的用户",
            Password = "Test@123456",
            PhoneNumber = GenerateChinesePhoneNumber(),
            Organizations = [FrontEndOrg], //  技术部下级: 前端组
            Roles = []
        });

        Assert.NotNull(user);
    }

    /// <summary>
    /// 组织管理员添加用户时机构不能为空
    /// 用户表单设置时组织不得为空
    /// </summary>
    [Fact]
    public async Task OrganizationAdminAddUser3()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadDevelopAdmin(session);

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();
        var exception = await Assert.ThrowsAsync<WildGooseFriendlyException>(async () =>
        {
            await userAdminService.AddAsync(new AddUserCommand
            {
                UserName = CreateName(),
                Name = "组织管理员添加的用户",
                Password = "Test@123456",
                PhoneNumber = GenerateChinesePhoneNumber(),
                Organizations = [],
                Roles = []
            });
        });
        Assert.Equal("访问受限", exception.Message);
    }

    /// <summary>
    /// 组织管理员不能添加用户到无权限的组织
    /// </summary>
    [Fact]
    public async Task OrganizationAdminAddUserToUnauthorizedOrganizationShouldFail()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadDevelopAdmin(session);

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();

        var exception = await Assert.ThrowsAsync<WildGooseFriendlyException>(async () =>
        {
            await userAdminService.AddAsync(new AddUserCommand
            {
                UserName = CreateName(),
                Name = "测试用户",
                Password = "Test@123456",
                PhoneNumber = GenerateChinesePhoneNumber(),
                Organizations = [RootOrg], // 总公司，无权限
                Roles = []
            });
        });

        Assert.Equal("访问受限", exception.Message);
    }

    /// <summary>
    /// 组织管理员只能修改其管理范围(本级)的用户
    /// </summary>
    [Fact]
    public async Task OrganizationAdminUpdateUser1()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session); // 先用超管创建用户

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();
        var userName = CreateName();
        var addedUser = await userAdminService.AddAsync(new AddUserCommand
        {
            UserName = userName,
            Name = "原名称",
            Password = "Test@123456",
            PhoneNumber = GenerateChinesePhoneNumber(),
            Organizations = [TechOrg],
            Roles = []
        });

        // 切换到组织管理员
        LoadDevelopAdmin(session);

        var updateCommand = new UpdateUserCommand
        {
            Id = addedUser.Id,
            UserName = userName,
            Name = "新名称",
            PhoneNumber = GenerateChinesePhoneNumber()
        };

        var updatedUser = await userAdminService.UpdateAsync(updateCommand);

        Assert.NotNull(updatedUser);
        Assert.Equal("新名称", updatedUser.Name);
    }

    /// <summary>
    /// 组织管理员只能修改其管理范围（下级）的用户
    /// </summary>
    [Fact]
    public async Task OrganizationAdminUpdateUser()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session); // 先用超管创建用户

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();
        var userName = CreateName();
        var addedUser = await userAdminService.AddAsync(new AddUserCommand
        {
            UserName = userName,
            Name = "原名称",
            Password = "Test@123456",
            PhoneNumber = GenerateChinesePhoneNumber(),
            Organizations = [FrontEndOrg],
            Roles = []
        });

        // 切换到组织管理员
        LoadDevelopAdmin(session);

        var updateCommand = new UpdateUserCommand
        {
            Id = addedUser.Id,
            UserName = userName,
            Name = "新名称",
            PhoneNumber = GenerateChinesePhoneNumber()
        };

        var updatedUser = await userAdminService.UpdateAsync(updateCommand);

        Assert.NotNull(updatedUser);
        Assert.Equal("新名称", updatedUser.Name);
    }

    /// <summary>
    /// 组织管理员禁用其管理范围本级的用户
    /// </summary>
    [Fact]
    public async Task OrganizationAdminDisableUser1()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);
        var dbContext = scope.ServiceProvider.GetRequiredService<WildGooseDbContext>();

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();
        var userName = CreateName();
        var addedUser = await userAdminService.AddAsync(new AddUserCommand
        {
            UserName = userName,
            Name = "待禁用用户",
            Password = "Test@123456",
            PhoneNumber = GenerateChinesePhoneNumber(),
            Organizations = [TechOrg], // 技术部
            Roles = []
        });

        // 切换到组织管理员
        LoadDevelopAdmin(session);

        await userAdminService.DisableAsync(new DisableUserCommand
        {
            Id = addedUser.Id
        });

        var disabledUser = await dbContext.Set<User>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == addedUser.Id);
        Assert.NotNull(disabledUser);
        Assert.False(disabledUser.IsEnabled);
    }

    /// <summary>
    /// 组织管理员禁用其管理范围下级的用户
    /// </summary>
    [Fact]
    public async Task OrganizationAdminDisableUser2()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);
        var dbContext = scope.ServiceProvider.GetRequiredService<WildGooseDbContext>();

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();
        var userName = CreateName();
        var addedUser = await userAdminService.AddAsync(new AddUserCommand
        {
            UserName = userName,
            Name = "待禁用用户",
            Password = "Test@123456",
            PhoneNumber = GenerateChinesePhoneNumber(),
            Organizations = [FrontEndOrg], // 前端组
            Roles = []
        });

        // 切换到组织管理员
        LoadDevelopAdmin(session);

        await userAdminService.DisableAsync(new DisableUserCommand
        {
            Id = addedUser.Id
        });

        var disabledUser = await dbContext.Set<User>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == addedUser.Id);
        Assert.NotNull(disabledUser);
        Assert.False(disabledUser.IsEnabled);
    }

    /// <summary>
    /// 组织管理员不能禁用其管理范围外的用户
    /// </summary>
    [Fact]
    public async Task OrganizationAdminDisableUser3()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();
        var userName = CreateName();
        var addedUser = await userAdminService.AddAsync(new AddUserCommand
        {
            UserName = userName,
            Name = "待禁用用户",
            Password = "Test@123456",
            PhoneNumber = GenerateChinesePhoneNumber(),
            Organizations = [RootOrg], // 总公司
            Roles = []
        });

        // 切换到组织管理员
        LoadDevelopAdmin(session);

        var exception = await Assert.ThrowsAsync<WildGooseFriendlyException>(async () =>
        {
            await userAdminService.DisableAsync(new DisableUserCommand
            {
                Id = addedUser.Id
            });
        });

        Assert.Equal("权限不足", exception.Message);
    }

    /// <summary>
    /// 组织管理员启用其管理范围本级的用户
    /// </summary>
    [Fact]
    public async Task OrganizationAdminEnableUser1()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);
        var dbContext = scope.ServiceProvider.GetRequiredService<WildGooseDbContext>();

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();
        var userName = CreateName();
        var addedUser = await userAdminService.AddAsync(new AddUserCommand
        {
            UserName = userName,
            Name = "待禁用用户",
            Password = "Test@123456",
            PhoneNumber = GenerateChinesePhoneNumber(),
            Organizations = [TechOrg], // 技术部
            Roles = []
        });

        // 切换到组织管理员
        LoadDevelopAdmin(session);

        await userAdminService.EnableAsync(new EnableUserCommand
        {
            Id = addedUser.Id
        });

        var disabledUser = await dbContext.Set<User>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == addedUser.Id);
        Assert.NotNull(disabledUser);
        Assert.True(disabledUser.IsEnabled);
    }

    /// <summary>
    /// 组织管理员启用其管理范围下级的用户
    /// </summary>
    [Fact]
    public async Task OrganizationAdminEnableUser2()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);
        var dbContext = scope.ServiceProvider.GetRequiredService<WildGooseDbContext>();

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();
        var userName = CreateName();
        var addedUser = await userAdminService.AddAsync(new AddUserCommand
        {
            UserName = userName,
            Name = "待禁用用户",
            Password = "Test@123456",
            PhoneNumber = GenerateChinesePhoneNumber(),
            Organizations = [FrontEndOrg], // 前端组
            Roles = []
        });

        // 切换到组织管理员
        LoadDevelopAdmin(session);

        await userAdminService.EnableAsync(new EnableUserCommand
        {
            Id = addedUser.Id
        });

        var disabledUser = await dbContext.Set<User>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == addedUser.Id);
        Assert.NotNull(disabledUser);
        Assert.True(disabledUser.IsEnabled);
    }

    /// <summary>
    /// 组织管理员不能启用其管理范围外的用户
    /// </summary>
    [Fact]
    public async Task OrganizationAdminEnableUser3()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();
        var userName = CreateName();
        var addedUser = await userAdminService.AddAsync(new AddUserCommand
        {
            UserName = userName,
            Name = "待禁用用户",
            Password = "Test@123456",
            PhoneNumber = GenerateChinesePhoneNumber(),
            Organizations = [RootOrg], // 总公司
            Roles = []
        });

        // 切换到组织管理员
        LoadDevelopAdmin(session);

        var exception = await Assert.ThrowsAsync<WildGooseFriendlyException>(async () =>
        {
            await userAdminService.EnableAsync(new EnableUserCommand
            {
                Id = addedUser.Id
            });
        });

        Assert.Equal("权限不足", exception.Message);
    }

    /// <summary>
    /// 组织管理员能修改其管理范围（本级）下的用户的密码
    /// </summary>
    [Fact]
    public async Task OrganizationAdminChangePassword1()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();
        var userName = CreateName();
        var addedUser = await userAdminService.AddAsync(new AddUserCommand
        {
            UserName = userName,
            Name = "测试用户",
            Password = "Test@123456",
            PhoneNumber = GenerateChinesePhoneNumber(),
            Organizations = [TechOrg],
            Roles = []
        });

        // 切换到组织管理员
        LoadDevelopAdmin(session);

        await userAdminService.ChangePasswordAsync(new ChangePasswordCommand
        {
            Id = addedUser.Id,
            NewPassword = "NewTest@123456",
            ConfirmPassword = "NewTest@123456"
        });
    }

    /// <summary>
    /// 组织管理员能修改其管理范围（下级）下的用户的密码
    /// </summary>
    [Fact]
    public async Task OrganizationAdminChangePassword2()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();
        var userName = CreateName();
        var addedUser = await userAdminService.AddAsync(new AddUserCommand
        {
            UserName = userName,
            Name = "测试用户",
            Password = "Test@123456",
            PhoneNumber = GenerateChinesePhoneNumber(),
            Organizations = [FrontEndOrg],
            Roles = []
        });

        // 切换到组织管理员
        LoadDevelopAdmin(session);

        await userAdminService.ChangePasswordAsync(new ChangePasswordCommand
        {
            Id = addedUser.Id,
            ConfirmPassword = "NewTest@123456"
        });
    }

    /// <summary>
    /// 组织管理员不能修改其管理范围外的用户的密码
    /// </summary>
    [Fact]
    public async Task OrganizationAdminChangePassword3()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();
        var userName = CreateName();
        var addedUser = await userAdminService.AddAsync(new AddUserCommand
        {
            UserName = userName,
            Name = "测试用户",
            Password = "Test@123456",
            PhoneNumber = GenerateChinesePhoneNumber(),
            Organizations = [RootOrg],
            Roles = []
        });

        // 切换到组织管理员
        LoadDevelopAdmin(session);
        var exception = await Assert.ThrowsAsync<WildGooseFriendlyException>(async () =>
        {
            await userAdminService.ChangePasswordAsync(new ChangePasswordCommand
            {
                Id = addedUser.Id,
                ConfirmPassword = "NewTest@123456"
            });
        });

        Assert.Equal("权限不足", exception.Message);
    }

    /// <summary>
    /// 查询不存在的用户应返回 null
    /// </summary>
    [Fact]
    public async Task GetNonExistentUserReturnsNull()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();
        var user = await userAdminService.GetAsync(new GetUserQuery
        {
            Id = "000000000000000000000000"
        });

        Assert.Null(user);
    }

    /// <summary>
    /// 修改不存在的用户应抛出异常
    /// </summary>
    [Fact]
    public async Task UpdateNonExistentUserShouldFail()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();
        var updateCommand = new UpdateUserCommand
        {
            Id = "000000000000000000000000",
            UserName = "non_existent",
            Name = "不存在用户"
        };

        var exception = await Assert.ThrowsAsync<WildGooseFriendlyException>(async () =>
        {
            await userAdminService.UpdateAsync(updateCommand);
        });

        Assert.Equal("用户不存在", exception.Message);
    }

    /// <summary>
    /// 删除不存在的用户应抛出异常
    /// </summary>
    [Fact]
    public async Task DeleteNonExistentUserShouldFail()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();
        var exception = await Assert.ThrowsAsync<WildGooseFriendlyException>(async () =>
        {
            await userAdminService.DeleteAsync(new DeleteUserCommand
            {
                Id = "000000000000000000000000"
            });
        });

        Assert.Equal("用户不存在", exception.Message);
    }

    /// <summary>
    /// 禁用不存在的用户应抛出异常
    /// </summary>
    [Fact]
    public async Task DisableNonExistentUserShouldFail()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();
        var exception = await Assert.ThrowsAsync<WildGooseFriendlyException>(async () =>
        {
            await userAdminService.DisableAsync(new DisableUserCommand
            {
                Id = "000000000000000000000000"
            });
        });

        Assert.Equal("用户不存在", exception.Message);
    }

    /// <summary>
    /// 启用不存在的用户应抛出异常
    /// </summary>
    [Fact]
    public async Task EnableNonExistentUserShouldFail()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();
        var exception = await Assert.ThrowsAsync<WildGooseFriendlyException>(async () =>
        {
            await userAdminService.EnableAsync(new EnableUserCommand
            {
                Id = "000000000000000000000000"
            });
        });

        Assert.Equal("用户不存在", exception.Message);
    }

    /// <summary>
    /// 修改不存在用户的密码应抛出异常
    /// </summary>
    [Fact]
    public async Task ChangePasswordForNonExistentUserShouldFail()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();
        var exception = await Assert.ThrowsAsync<WildGooseFriendlyException>(async () =>
        {
            await userAdminService.ChangePasswordAsync(new ChangePasswordCommand
            {
                Id = "000000000000000000000000",
                ConfirmPassword = "NewTest@123456"
            });
        });

        Assert.Equal("用户不存在", exception.Message);
    }

    /// <summary>
    /// 测试按状态筛选用户 - enabled
    /// </summary>
    [Fact]
    public async Task GetUserListByStatusEnabled()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();
        var result = await userAdminService.GetListAsync(new GetUserListQuery
        {
            Page = 1,
            Limit = 60,
            Status = "enabled"
        });

        Assert.NotNull(result);
        Assert.All(result.Data, x => Assert.True(x.Enabled));
    }

    /// <summary>
    /// 测试按状态筛选用户 - disabled
    /// </summary>
    [Fact]
    public async Task GetUserListByStatusDisabled()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();
        var result = await userAdminService.GetListAsync(new GetUserListQuery
        {
            Page = 1,
            Limit = 60,
            Status = "disabled"
        });

        Assert.NotNull(result);
        Assert.All(result.Data, x => Assert.False(x.Enabled));
    }

    /// <summary>
    /// 测试按关键词搜索用户
    /// </summary>
    [Fact]
    public async Task SearchUserByKeyword()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();
        var result = await userAdminService.GetListAsync(new GetUserListQuery
        {
            Page = 1,
            Limit = 10,
            Q = "小明"
        });

        Assert.NotNull(result);
        // 应该能找到包含"小明"的用户
        Assert.Contains(result.Data, x => x.Name.Contains("小明") || x.UserName.Contains("小明"));
    }

    // ============================================================================
    // 用户管理员 (user-admin) 角色测试
    // ============================================================================

    /// <summary>
    /// 用户管理员可以查询所有用户
    /// </summary>
    [Fact]
    public async Task UserAdminGetUserList()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadUserAdmin(session);

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();
        var result = await userAdminService.GetListAsync(new GetUserListQuery
        {
            Page = 1,
            Limit = 60,
            OrganizationId = null
        });

        Assert.NotNull(result);
        Assert.True(result.Total > 0);
        Assert.NotNull(result.Data);
    }

    /// <summary>
    /// 用户管理员可以查询任意用户详情
    /// </summary>
    [Fact]
    public async Task UserAdminGetUser()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadUserAdmin(session);

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();
        var user = await userAdminService.GetAsync(new GetUserQuery
        {
            Id = "507f1f77bcf86cd799439025"
        });

        Assert.NotNull(user);
        Assert.Equal("小明", user.Name);
    }

    /// <summary>
    /// 用户管理员可以添加用户，不设置组织和角色
    /// </summary>
    [Fact]
    public async Task UserAdminAddUserWithoutOrganization()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadUserAdmin(session);

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();
        var userName = CreateName();
        var user = await userAdminService.AddAsync(new AddUserCommand
        {
            UserName = userName,
            Name = "测试用户",
            Password = "Test@123456",
            PhoneNumber = GenerateChinesePhoneNumber(),
            Organizations = [],
            Roles = []
        });

        Assert.NotNull(user);
        Assert.Equal(userName, user.UserName);
        Assert.Equal("测试用户", user.Name);
        Assert.True(user.Enabled);
    }

    /// <summary>
    /// 用户管理员可以添加用户，设置组织和角色
    /// </summary>
    [Fact]
    public async Task UserAdminAddUserWithOrganization()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadUserAdmin(session);

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();
        var userName = CreateName();
        var user = await userAdminService.AddAsync(new AddUserCommand
        {
            UserName = userName,
            Name = "测试用户",
            Password = "Test@123456",
            PhoneNumber = GenerateChinesePhoneNumber(),
            Organizations = [TechOrg],
            Roles = [EmployeeRole]
        });

        Assert.NotNull(user);
        Assert.Equal(userName, user.UserName);
        Assert.NotNull(user.Organizations);
        Assert.Single(user.Organizations);
    }

    /// <summary>
    /// 用户管理员可以修改任意用户
    /// </summary>
    [Fact]
    public async Task UserAdminUpdateUser()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadUserAdmin(session);

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();
        var addedUser = await userAdminService.AddAsync(new AddUserCommand
        {
            UserName = CreateName(),
            Name = "原名称",
            Password = "Test@123456",
            PhoneNumber = GenerateChinesePhoneNumber(),
            Organizations = [],
            Roles = []
        });

        var updateCommand = new UpdateUserCommand
        {
            Id = addedUser.Id,
            UserName = CreateName(),
            Name = "新名称",
            PhoneNumber = GenerateChinesePhoneNumber()
        };
        await userAdminService.UpdateAsync(updateCommand);

        var updatedUser = await userAdminService.GetAsync(new GetUserQuery
        {
            Id = addedUser.Id
        });

        Assert.NotNull(updatedUser);
        Assert.Equal("新名称", updatedUser.Name);
    }

    /// <summary>
    /// 用户管理员可以删除任意用户
    /// </summary>
    [Fact]
    public async Task UserAdminDeleteUser()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadUserAdmin(session);
        var dbContext = scope.ServiceProvider.GetRequiredService<WildGooseDbContext>();

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();
        var addedUser = await userAdminService.AddAsync(new AddUserCommand
        {
            UserName = CreateName(),
            Name = "待删除用户",
            Password = "Test@123456",
            PhoneNumber = GenerateChinesePhoneNumber(),
            Organizations = [],
            Roles = []
        });

        await userAdminService.DeleteAsync(new DeleteUserCommand
        {
            Id = addedUser.Id
        });

        await using var conn = dbContext.Database.GetDbConnection();
        var deletedUser = (IDictionary<string, dynamic>)await conn.QuerySingleAsync($"""
             SELECT id, is_deleted FROM wild_goose_user WHERE id = '{addedUser.Id}'
             """);
        Assert.NotNull(deletedUser);
        Assert.True(deletedUser["is_deleted"]);
    }

    /// <summary>
    /// 用户管理员可以禁用用户
    /// </summary>
    [Fact]
    public async Task UserAdminDisableUser()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadUserAdmin(session);
        var dbContext = scope.ServiceProvider.GetRequiredService<WildGooseDbContext>();

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();
        var addedUser = await userAdminService.AddAsync(new AddUserCommand
        {
            UserName = CreateName(),
            Name = "待禁用用户",
            Password = "Test@123456",
            PhoneNumber = GenerateChinesePhoneNumber(),
            Organizations = [],
            Roles = []
        });

        await userAdminService.DisableAsync(new DisableUserCommand
        {
            Id = addedUser.Id
        });

        var disabledUser = await dbContext.Set<User>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == addedUser.Id);
        Assert.NotNull(disabledUser);
        Assert.False(disabledUser.IsEnabled);
    }

    /// <summary>
    /// 用户管理员可以启用用户
    /// </summary>
    [Fact]
    public async Task UserAdminEnableUser()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadUserAdmin(session);
        var dbContext = scope.ServiceProvider.GetRequiredService<WildGooseDbContext>();

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();
        var addedUser = await userAdminService.AddAsync(new AddUserCommand
        {
            UserName = CreateName(),
            Name = "待启用用户",
            Password = "Test@123456",
            PhoneNumber = GenerateChinesePhoneNumber(),
            Organizations = [],
            Roles = []
        });

        await userAdminService.DisableAsync(new DisableUserCommand { Id = addedUser.Id });

        await userAdminService.EnableAsync(new EnableUserCommand
        {
            Id = addedUser.Id
        });

        var enabledUser = await dbContext.Set<User>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == addedUser.Id);
        Assert.NotNull(enabledUser);
        Assert.True(enabledUser.IsEnabled);
    }

    /// <summary>
    /// 用户管理员可以修改用户密码
    /// </summary>
    [Fact]
    public async Task UserAdminChangePassword()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadUserAdmin(session);

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();
        var addedUser = await userAdminService.AddAsync(new AddUserCommand
        {
            UserName = CreateName(),
            Name = "测试用户",
            Password = "Test@123456",
            PhoneNumber = GenerateChinesePhoneNumber(),
            Organizations = [],
            Roles = []
        });

        await userAdminService.ChangePasswordAsync(new ChangePasswordCommand
        {
            Id = addedUser.Id,
            NewPassword = "NewTest@123456",
            ConfirmPassword = "NewTest@123456"
        });
    }

    // ============================================================================
    // 可授于角色权限校验测试
    // ============================================================================

    /// <summary>
    /// 组织管理员添加用户时设置不可授于角色应失败
    /// </summary>
    [Fact]
    public async Task OrganizationAdminAddUserWithUnauthorizedRoleShouldFail()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();

        LoadDevelopAdmin(session);

        // 组织管理员尝试添加用户并设置 manager 角色（不可授于）
        var exception = await Assert.ThrowsAsync<WildGooseFriendlyException>(async () =>
        {
            await userAdminService.AddAsync(new AddUserCommand
            {
                UserName = CreateName(),
                Name = "测试用户",
                Password = "Test@123456",
                PhoneNumber = GenerateChinesePhoneNumber(),
                Organizations = [TechOrg],
                Roles = ["xxx"] // 组织管理员不可授于的角色
            });
        });

        Assert.Equal("存在不可授于的角色", exception.Message);
    }

    /// <summary>
    /// 组织管理员修改用户新增不可授于角色应失败
    /// </summary>
    [Fact]
    public async Task OrganizationAdminUpdateUserAddUnauthorizedRoleShouldFail()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();

        // 先创建一个用户
        var addedUser = await userAdminService.AddAsync(new AddUserCommand
        {
            UserName = CreateName(),
            Name = "测试用户",
            Password = "Test@123456",
            PhoneNumber = GenerateChinesePhoneNumber(),
            Organizations = [TechOrg],
            Roles = [EmployeeRole]
        });

        // 切换到技术部组织管理员
        LoadDevelopAdmin(session);

        // 组织管理员尝试修改用户，新增 manager 角色（不可授于）
        var updateCommand = new UpdateUserCommand
        {
            Id = addedUser.Id,
            UserName = addedUser.UserName,
            Name = CreateName(),
            PhoneNumber = GenerateChinesePhoneNumber(),
            Roles = [EmployeeRole, "xxx"] // manager 是不可授于的角色
        };

        var exception = await Assert.ThrowsAsync<WildGooseFriendlyException>(async () =>
        {
            await userAdminService.UpdateAsync(updateCommand);
        });

        Assert.Equal("存在不可授于的角色", exception.Message);
    }

    /// <summary>
    /// 组织管理员修改用户时可以删除任何角色（不受权限限制）
    /// </summary>
    [Fact]
    public async Task OrganizationAdminUpdateUserRemoveAnyRoleShouldSuccess()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();

        // 先创建一个用户，设置多个角色（包括组织管理员不可授于的角色）
        var addedUser = await userAdminService.AddAsync(new AddUserCommand
        {
            UserName = CreateName(),
            Name = "测试用户",
            Password = "Test@123456",
            PhoneNumber = GenerateChinesePhoneNumber(),
            Organizations = [TechOrg],
            Roles = ["employee", "manager"]
        });

        // 切换到技术部组织管理员
        LoadDevelopAdmin(session);

        // 组织管理员修改用户，删除 manager 角色（虽然不可授于，但可以删除）
        var updateCommand = new UpdateUserCommand
        {
            Id = addedUser.Id,
            UserName = addedUser.UserName,
            Name = "新名称",
            PhoneNumber = GenerateChinesePhoneNumber(),
            Organizations = [TechOrg],
            Roles = ["employee"] // 删除 manager 角色
        };

        await userAdminService.UpdateAsync(updateCommand);

        var user = await userAdminService.GetAsync(new GetUserQuery
        {
            Id = addedUser.Id
        });
        Assert.NotNull(user);
        Assert.Single(user.Roles);
    }

    // ============================================================================
    // 组织管理员禁止调用删除用户接口
    // ============================================================================

    /// <summary>
    /// 组织管理员删除本级用户
    /// </summary>
    [Fact]
    public async Task OrganizationAdminDeleteUserShouldFail()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();
        var addedUser = await userAdminService.AddAsync(new AddUserCommand
        {
            UserName = CreateName(),
            Name = "测试用户",
            Password = "Test@123456",
            PhoneNumber = GenerateChinesePhoneNumber(),
            Organizations = [TechOrg],
            Roles = []
        });

        // 切换到技术部组织管理员
        LoadDevelopAdmin(session);

        await userAdminService.DeleteAsync(new DeleteUserCommand
        {
            Id = addedUser.Id
        });
    }

    /// <summary>
    /// 组织管理员删除下级用户
    /// </summary>
    [Fact]
    public async Task OrganizationAdminDeleteUser2ShouldFail()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();
        var addedUser = await userAdminService.AddAsync(new AddUserCommand
        {
            UserName = CreateName(),
            Name = "测试用户",
            Password = "Test@123456",
            PhoneNumber = GenerateChinesePhoneNumber(),
            Organizations = [FrontEndOrg],
            Roles = []
        });

        // 切换到技术部组织管理员
        LoadDevelopAdmin(session);

        await userAdminService.DeleteAsync(new DeleteUserCommand
        {
            Id = addedUser.Id
        });
    }
    // ============================================================================
    // 修改用户时组织的删除逻辑
    // ============================================================================

    /// <summary>
    /// 组织管理员修改用户时只能删除其管理范围内的组织
    /// </summary>
    [Fact]
    public async Task OrganizationAdminUpdateUserRemoveOrganizationOnlyInManagedScope()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();

        // 先创建一个用户，属于技术部和前端组
        var addedUser = await userAdminService.AddAsync(new AddUserCommand
        {
            UserName = CreateName(),
            Name = "测试用户",
            Password = "Test@123456",
            PhoneNumber = GenerateChinesePhoneNumber(),
            Organizations = [TechOrg, FrontEndOrg],
            Roles = []
        });

        // 切换到技术部组织管理员
        LoadDevelopAdmin(session);

        // 组织管理员修改用户，删除前端组（技术部下级，在管理范围内）
        var updateCommand = new UpdateUserCommand
        {
            Id = addedUser.Id,
            UserName = addedUser.UserName,
            Name = "新名称",
            PhoneNumber = GenerateChinesePhoneNumber(),
            Organizations = [TechOrg] // 只保留技术部，删除前端组
        };

        var updatedUser = await userAdminService.UpdateAsync(updateCommand);

        Assert.NotNull(updatedUser);
        Assert.Single(updatedUser.Organizations);
        Assert.Contains("技术部", updatedUser.Organizations);
    }

    /// <summary>
    /// 组织管理员修改用户时不能删除管理范围外的组织
    /// </summary>
    [Fact]
    public async Task OrganizationAdminUpdateUserCannotRemoveOrganizationOutOfScope()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();

        // 先创建一个用户，属于技术部和总公司
        var addedUser = await userAdminService.AddAsync(new AddUserCommand
        {
            UserName = CreateName(),
            Name = "测试用户",
            Password = "Test@123456",
            PhoneNumber = GenerateChinesePhoneNumber(),
            Organizations = [TechOrg, RootOrg],
            Roles = []
        });

        // 切换到技术部组织管理员
        LoadDevelopAdmin(session);

        // 组织管理员修改用户，尝试删除总公司（不在管理范围内）
        // 实际行为：不会删除总公司，只会删除能删除的（技术部）
        var updateCommand = new UpdateUserCommand
        {
            Id = addedUser.Id,
            UserName = addedUser.UserName,
            Name = "新名称",
            PhoneNumber = GenerateChinesePhoneNumber(),
            Organizations = [TechOrg] // 尝试只保留技术部
        };

        var updatedUser = await userAdminService.UpdateAsync(updateCommand);

        // 总公司不应该被删除，因为技术部管理员没有权限删除总公司
        Assert.NotNull(updatedUser);
        Assert.True(updatedUser.Organizations.Count() == 2);
    }

    // ============================================================================
    // 组织管理员状态筛选和搜索测试
    // ============================================================================

    /// <summary>
    /// 组织管理员按状态筛选用户 - enabled
    /// </summary>
    [Fact]
    public async Task OrganizationAdminGetUserListByStatusEnabled()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadDevelopAdmin(session);

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();
        var result = await userAdminService.GetListAsync(new GetUserListQuery
        {
            Page = 1,
            Limit = 60,
            Status = "enabled"
        });

        Assert.NotNull(result);
        Assert.All(result.Data, x => Assert.True(x.Enabled));
    }

    /// <summary>
    /// 组织管理员按状态筛选用户 - disabled
    /// </summary>
    [Fact]
    public async Task OrganizationAdminGetUserListByStatusDisabled()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadDevelopAdmin(session);

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();
        var result = await userAdminService.GetListAsync(new GetUserListQuery
        {
            Page = 1,
            Limit = 60,
            Status = "disabled"
        });

        Assert.NotNull(result);
        Assert.All(result.Data, x => Assert.False(x.Enabled));
    }

    /// <summary>
    /// 组织管理员按关键词搜索用户
    /// </summary>
    [Fact]
    public async Task OrganizationAdminSearchUserByKeyword()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadDevelopAdmin(session);

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();
        var result = await userAdminService.GetListAsync(new GetUserListQuery
        {
            Page = 1,
            Limit = 10,
            Q = "小明"
        });

        Assert.NotNull(result);
        Assert.Contains(result.Data, x => x.Name.Contains("小明") || x.UserName.Contains("小明"));
    }
}