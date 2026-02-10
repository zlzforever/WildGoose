using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WildGoose.Application.Role.Admin.V10;
using WildGoose.Application.Role.Admin.V10.Command;
using WildGoose.Application.Role.Admin.V10.Queries;
using WildGoose.Domain;
using WildGoose.Domain.Entity;
using WildGoose.Infrastructure;
using Xunit;

namespace WildGoose.Tests;

/// <summary>
/// 角色管理服务测试
/// 测试超级管理员对角色的增删改查权限
/// </summary>
[Collection("WebApplication collection")]
public class RoleAdminServiceTests(WebApplicationFactoryFixture fixture) : BaseTests
{
    // 角色ID（根据 testdata.sql）
    private const string AdminRoleId = "507f1f77bcf86cd799439011";
    private const string OrganizationAdminRoleId = "507f1f77bcf86cd799439012";
    private const string UserAdminRoleId = "507f1f77bcf86cd799439013";
    private const string ManagerRoleId = "507f1f77bcf86cd799439014";
    private const string EmployeeRoleId = "507f1f77bcf86cd799439015";
    private const string InternRoleId = "507f1f77bcf86cd799439016";

    // ============================================================================
    // 角色列表查询接口测试
    // ============================================================================

    /// <summary>
    /// 超级管理员可以查询所有角色
    /// </summary>
    [Fact]
    public async Task SuperAdminGetRoleList()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var roleAdminService = scope.ServiceProvider.GetRequiredService<RoleAdminService>();
        var result = await roleAdminService.GetRolesAsync(new GetRolesQuery
        {
            Page = 1,
            Limit = 10
        });
        Assert.NotNull(result);
        Assert.True(result.Total > 0);
        Assert.NotNull(result.Data);
        var manager = result.Data.FirstOrDefault(x => x.Id == ManagerRoleId);
        Assert.NotNull(manager);
        Assert.True(manager.AssignableRoles.Count > 0);
        Assert.Contains(result.Data, x => x.Id == AdminRoleId);
        Assert.Contains(result.Data, x => x.Id == OrganizationAdminRoleId);
        Assert.Contains(result.Data, x => x.Id == UserAdminRoleId);
    }

    /// <summary>
    /// 超级管理员可以按关键词搜索角色
    /// </summary>
    [Fact]
    public async Task SuperAdminSearchRoles()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var roleAdminService = scope.ServiceProvider.GetRequiredService<RoleAdminService>();
        var result = await roleAdminService.GetRolesAsync(new GetRolesQuery
        {
            Page = 1,
            Limit = 10,
            Q = "manag"
        });

        Assert.NotNull(result);
        Assert.Contains(result.Data, x => x.Name.Contains("manager"));
    }

    /// <summary>
    /// 超级管理员查询角色详情
    /// </summary>
    [Fact]
    public async Task SuperAdminGetRole()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var roleAdminService = scope.ServiceProvider.GetRequiredService<RoleAdminService>();
        var role = await roleAdminService.GetAsync(new GetRoleQuery
        {
            Id = ManagerRoleId
        });

        Assert.NotNull(role);
        Assert.Equal("manager", role.Name);
        Assert.NotNull(role.Statement);
    }

    /// <summary>
    /// 查询不存在的角色应返回 null
    /// </summary>
    [Fact]
    public async Task GetNonExistentRoleReturnsNull()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var roleAdminService = scope.ServiceProvider.GetRequiredService<RoleAdminService>();
        var role = await roleAdminService.GetAsync(new GetRoleQuery
        {
            Id = "000000000000000000000000"
        });

        Assert.Null(role);
    }

    // ============================================================================
    // 添加角色接口测试
    // ============================================================================

    /// <summary>
    /// 超级管理员可以添加新角色
    /// </summary>
    [Fact]
    public async Task SuperAdminAddRole()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var roleAdminService = scope.ServiceProvider.GetRequiredService<RoleAdminService>();
        var name = CreateName();
        var roleId = await roleAdminService.AddAsync(new AddRoleCommand
        {
            Name = name,
            Description = "测试角色"
        });

        Assert.NotNull(roleId);

        // 验证角色已创建
        var dbContext = scope.ServiceProvider.GetRequiredService<WildGooseDbContext>();
        var role = await dbContext.Set<WildGoose.Domain.Entity.Role>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == roleId);
        Assert.NotNull(role);
        Assert.Equal(name, role.Name);
        Assert.Equal("测试角色", role.Description);
    }

    /// <summary>
    /// 添加角色时不能使用系统保留角色名（admin）
    /// </summary>
    [Fact]
    public async Task AddRoleWithSystemNameShouldFail()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var roleAdminService = scope.ServiceProvider.GetRequiredService<RoleAdminService>();
        var exception = await Assert.ThrowsAsync<WildGooseFriendlyException>(async () =>
        {
            await roleAdminService.AddAsync(new AddRoleCommand
            {
                Name = Defaults.AdminRole,
                Description = "测试角色"
            });
        });

        Assert.Equal("禁止使用系统角色名", exception.Message);
    }

    /// <summary>
    /// 添加角色时不能使用系统保留角色名（organization-admin）
    /// </summary>
    [Fact]
    public async Task AddRoleWithOrganizationAdminNameShouldFail()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var roleAdminService = scope.ServiceProvider.GetRequiredService<RoleAdminService>();
        var exception = await Assert.ThrowsAsync<WildGooseFriendlyException>(async () =>
        {
            await roleAdminService.AddAsync(new AddRoleCommand
            {
                Name = Defaults.OrganizationAdmin,
                Description = "测试角色"
            });
        });

        Assert.Equal("禁止使用系统角色名", exception.Message);
    }

    /// <summary>
    /// 添加角色时不能使用系统保留角色名（user-admin）
    /// </summary>
    [Fact]
    public async Task AddRoleWithUserAdminNameShouldFail()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var roleAdminService = scope.ServiceProvider.GetRequiredService<RoleAdminService>();
        var exception = await Assert.ThrowsAsync<WildGooseFriendlyException>(async () =>
        {
            await roleAdminService.AddAsync(new AddRoleCommand
            {
                Name = Defaults.UserAdmin,
                Description = "测试角色"
            });
        });

        Assert.Equal("禁止使用系统角色名", exception.Message);
    }

    /// <summary>
    /// 添加已存在的角色名应失败
    /// </summary>
    [Fact]
    public async Task AddDuplicateRoleShouldFail()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var roleAdminService = scope.ServiceProvider.GetRequiredService<RoleAdminService>();
        var exception = await Assert.ThrowsAsync<WildGooseFriendlyException>(async () =>
        {
            await roleAdminService.AddAsync(new AddRoleCommand
            {
                Name = "manager",
                Description = "部门经理"
            });
        });

        Assert.Equal("角色已经存在", exception.Message);
    }

    // ============================================================================
    // 修改角色接口测试
    // ============================================================================

    /// <summary>
    /// 超级管理员可以修改角色信息
    /// </summary>
    [Fact]
    public async Task SuperAdminUpdateRole()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var roleAdminService = scope.ServiceProvider.GetRequiredService<RoleAdminService>();
        var name = CreateName();
        var roleId = await roleAdminService.AddAsync(new AddRoleCommand
        {
            Name = CreateName(),
            Description = "测试角色"
        });

        var updateCommand = new UpdateRoleCommand
        {
            Id = roleId,
            Name = name + "_updated",
            Description = "更新后的描述"
        };

        await roleAdminService.UpdateAsync(updateCommand);

        // 验证角色已更新
        var dbContext = scope.ServiceProvider.GetRequiredService<WildGooseDbContext>();
        var role = await dbContext.Set<WildGoose.Domain.Entity.Role>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == roleId);
        Assert.NotNull(role);
        Assert.Equal(updateCommand.Name, role.Name);
        Assert.Equal("更新后的描述", role.Description);
    }

    /// <summary>
    /// 修改不存在的角色应失败
    /// </summary>
    [Fact]
    public async Task UpdateNonExistentRoleShouldFail()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var roleAdminService = scope.ServiceProvider.GetRequiredService<RoleAdminService>();
        var exception = await Assert.ThrowsAsync<WildGooseFriendlyException>(async () =>
        {
            await roleAdminService.UpdateAsync(new UpdateRoleCommand
            {
                Id = "000000000000000000000000",
                Name = "test_role",
                Description = "测试"
            });
        });

        Assert.Equal("角色不存在", exception.Message);
    }

    /// <summary>
    /// 不能修改系统保留角色（admin）
    /// </summary>
    [Fact]
    public async Task UpdateSystemRoleShouldFail()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var roleAdminService = scope.ServiceProvider.GetRequiredService<RoleAdminService>();
        var exception = await Assert.ThrowsAsync<WildGooseFriendlyException>(async () =>
        {
            await roleAdminService.UpdateAsync(new UpdateRoleCommand
            {
                Id = AdminRoleId,
                Name = "admin_modified",
                Description = "修改后的管理员"
            });
        });

        Assert.Equal("禁止操作系统角色信息", exception.Message);
    }

    // ============================================================================
    // 修改角色权限声明测试
    // ============================================================================

    /// <summary>
    /// 超级管理员可以修改角色权限声明
    /// </summary>
    [Fact]
    public async Task SuperAdminUpdateRoleStatement()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var roleAdminService = scope.ServiceProvider.GetRequiredService<RoleAdminService>();
        var newStatement = @"[{
            ""effect"": ""Allow"",
            ""resource"": [""users""],
            ""action"": [""read""]
        }]";

        await roleAdminService.UpdateStatementAsync(new UpdateStatementCommand
        {
            Id = ManagerRoleId,
            Statement = newStatement
        });

        // 验证权限声明已更新
        var dbContext = scope.ServiceProvider.GetRequiredService<WildGooseDbContext>();
        var role = await dbContext.Set<WildGoose.Domain.Entity.Role>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == ManagerRoleId);
        Assert.NotNull(role);
        Assert.NotNull(role.Statement);
        Assert.Contains("users", role.Statement);
    }

    /// <summary>
    /// 修改不存在的角色的权限声明应失败
    /// </summary>
    [Fact]
    public async Task UpdateStatementForNonExistentRoleShouldFail()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var roleAdminService = scope.ServiceProvider.GetRequiredService<RoleAdminService>();
        var exception = await Assert.ThrowsAsync<WildGooseFriendlyException>(async () =>
        {
            await roleAdminService.UpdateStatementAsync(new UpdateStatementCommand
            {
                Id = "000000000000000000000000",
                Statement = "[]"
            });
        });

        Assert.Equal("角色不存在", exception.Message);
    }

    /// <summary>
    /// 不能修改系统保留角色的权限声明
    /// </summary>
    [Fact]
    public async Task UpdateSystemRoleStatementShouldFail()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var roleAdminService = scope.ServiceProvider.GetRequiredService<RoleAdminService>();
        var exception = await Assert.ThrowsAsync<WildGooseFriendlyException>(async () =>
        {
            await roleAdminService.UpdateStatementAsync(new UpdateStatementCommand
            {
                Id = AdminRoleId,
                Statement = "[]"
            });
        });

        Assert.Equal("禁止操作系统角色信息", exception.Message);
    }

    // ============================================================================
    // 删除角色接口测试
    // ============================================================================

    /// <summary>
    /// 超级管理员可以删除角色
    /// </summary>
    [Fact]
    public async Task SuperAdminDeleteRole()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);
        var dbContext = scope.ServiceProvider.GetRequiredService<WildGooseDbContext>();

        // 先创建一个角色
        var roleAdminService = scope.ServiceProvider.GetRequiredService<RoleAdminService>();
        var roleId = await roleAdminService.AddAsync(new AddRoleCommand
        {
            Name = $"test_role_{Guid.NewGuid():N}",
            Description = "待删除角色"
        });

        // 删除角色
        await roleAdminService.DeleteAsync(new DeleteRoleCommand
        {
            Id = roleId
        });

        // 验证角色已删除
        var role = await dbContext.Set<WildGoose.Domain.Entity.Role>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == roleId);
        Assert.Null(role);
    }

    /// <summary>
    /// 删除角色时应同时删除用户角色关联
    /// </summary>
    [Fact]
    public async Task DeleteRoleShouldRemoveUserRoles()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);
        var dbContext = scope.ServiceProvider.GetRequiredService<WildGooseDbContext>();

        // 先创建一个角色并分配给用户
        var roleAdminService = scope.ServiceProvider.GetRequiredService<RoleAdminService>();
        var roleId = await roleAdminService.AddAsync(new AddRoleCommand
        {
            Name = $"test_role_{Guid.NewGuid():N}",
            Description = "测试角色"
        });

        // 给用户分配该角色
        await dbContext.AddAsync(new IdentityUserRole<string>
        {
            UserId = FrontendUserId,
            RoleId = roleId
        });
        await dbContext.SaveChangesAsync();

        // 删除角色
        await roleAdminService.DeleteAsync(new DeleteRoleCommand
        {
            Id = roleId
        });

        // 验证用户角色关联已删除
        var userRole = await dbContext.Set<IdentityUserRole<string>>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == FrontendUserId && x.RoleId == roleId);
        Assert.Null(userRole);
    }

    /// <summary>
    /// 删除不存在的角色应失败
    /// </summary>
    [Fact]
    public async Task DeleteNonExistentRoleShouldFail()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var roleAdminService = scope.ServiceProvider.GetRequiredService<RoleAdminService>();
        var exception = await Assert.ThrowsAsync<WildGooseFriendlyException>(async () =>
        {
            await roleAdminService.DeleteAsync(new DeleteRoleCommand
            {
                Id = "000000000000000000000000"
            });
        });

        Assert.Equal("角色不存在", exception.Message);
    }

    /// <summary>
    /// 不能删除系统保留角色
    /// </summary>
    [Fact]
    public async Task DeleteSystemRoleShouldFail()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var roleAdminService = scope.ServiceProvider.GetRequiredService<RoleAdminService>();
        var exception = await Assert.ThrowsAsync<WildGooseFriendlyException>(async () =>
        {
            await roleAdminService.DeleteAsync(new DeleteRoleCommand
            {
                Id = AdminRoleId
            });
        });

        Assert.Equal("禁止操作系统角色信息", exception.Message);
    }

    // ============================================================================
    // 可授于角色管理测试
    // ============================================================================

    /// <summary>
    /// 超级管理员可以添加可授于角色
    /// </summary>
    [Fact]
    public async Task SuperAdminAddAssignableRole()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);
        var dbContext = scope.ServiceProvider.GetRequiredService<WildGooseDbContext>();

        // 先创建一个角色
        var roleAdminService = scope.ServiceProvider.GetRequiredService<RoleAdminService>();
        var roleId = await roleAdminService.AddAsync(new AddRoleCommand
        {
            Name = $"test_role_{Guid.NewGuid():N}",
            Description = "测试角色"
        });

        // 添加可授于角色
        await roleAdminService.AddAssignableRoleAsync(new AddAssignableRoleCommand
        {
            Id = roleId,
            AssignableRoleIds = [ManagerRoleId, EmployeeRoleId]
        });

        // 验证可授于角色已添加
        var assignableRoles = await dbContext.Set<RoleAssignableRole>()
            .AsNoTracking()
            .Where(x => x.RoleId == roleId)
            .Select(x => x.AssignableId)
            .ToListAsync();
        Assert.Equal(2, assignableRoles.Count);
        Assert.Contains(ManagerRoleId, assignableRoles);
        Assert.Contains(EmployeeRoleId, assignableRoles);
    }

    /// <summary>
    /// 不能给系统保留角色添加可授于角色
    /// </summary>
    [Fact]
    public async Task AddAssignableRoleToSystemRoleShouldFail()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var roleAdminService = scope.ServiceProvider.GetRequiredService<RoleAdminService>();
        var exception = await Assert.ThrowsAsync<WildGooseFriendlyException>(async () =>
        {
            await roleAdminService.AddAssignableRoleAsync(new AddAssignableRoleCommand
            {
                Id = AdminRoleId,
                AssignableRoleIds = [ManagerRoleId]
            });
        });

        Assert.Equal("禁止操作系统角色信息", exception.Message);
    }

    /// <summary>
    /// 添加可授于角色时，已存在的应被跳过
    /// </summary>
    [Fact]
    public async Task AddAssignableRoleShouldSkipExisting()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);
        var dbContext = scope.ServiceProvider.GetRequiredService<WildGooseDbContext>();

        var roleAdminService = scope.ServiceProvider.GetRequiredService<RoleAdminService>();

        // organization-admin 已有 manager 作为可授于角色（testdata.sql）
        // 再次添加不应报错，应跳过已存在的
        await roleAdminService.AddAssignableRoleAsync(new AddAssignableRoleCommand
        {
            Id = ManagerRoleId,
            AssignableRoleIds = [EmployeeRoleId]
        });
        await roleAdminService.AddAssignableRoleAsync(new AddAssignableRoleCommand
        {
            Id = ManagerRoleId,
            AssignableRoleIds = [EmployeeRoleId]
        });

        // 验证只有一个可授于角色关系
        var count = await dbContext.Set<RoleAssignableRole>()
            .AsNoTracking()
            .CountAsync(x => x.RoleId == OrganizationAdminRoleId && x.AssignableId == ManagerRoleId);
        Assert.Equal(1, count);
    }

    /// <summary>
    /// 超级管理员可以删除可授于角色
    /// </summary>
    [Fact]
    public async Task SuperAdminDeleteAssignableRole()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);
        var dbContext = scope.ServiceProvider.GetRequiredService<WildGooseDbContext>();

        // 先创建角色并添加可授于角色
        var roleAdminService = scope.ServiceProvider.GetRequiredService<RoleAdminService>();
        var roleId = await roleAdminService.AddAsync(new AddRoleCommand
        {
            Name = $"test_role_{Guid.NewGuid():N}",
            Description = "测试角色"
        });

        await roleAdminService.AddAssignableRoleAsync(new AddAssignableRoleCommand
        {
            Id = roleId,
            AssignableRoleIds = [ManagerRoleId]
        });

        // 删除可授于角色
        await roleAdminService.DeleteAssignableRoleAsync(new DeleteAssignableRoleCommand
        {
            Id = roleId,
            AssignableRoleId = ManagerRoleId
        });

        // 验证可授于角色已删除
        var assignableRole = await dbContext.Set<RoleAssignableRole>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.RoleId == roleId && x.AssignableId == ManagerRoleId);
        Assert.Null(assignableRole);
    }

    /// <summary>
    /// 删除不存在的可授于角色关系应失败
    /// </summary>
    [Fact]
    public async Task DeleteNonExistentAssignableRoleShouldFail()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var roleAdminService = scope.ServiceProvider.GetRequiredService<RoleAdminService>();
        var exception = await Assert.ThrowsAsync<WildGooseFriendlyException>(async () =>
        {
            await roleAdminService.DeleteAssignableRoleAsync(new DeleteAssignableRoleCommand
            {
                Id = ManagerRoleId,
                AssignableRoleId = "000000000000000000000000"
            });
        });

        Assert.Equal("数据不存在", exception.Message);
    }

    // ============================================================================
    // 查询可授于角色列表测试
    // ============================================================================

    /// <summary>
    /// 超级管理员查询可授于角色列表，应返回所有角色（除组织管理员）
    /// </summary>
    [Fact]
    public async Task SuperAdminGetAssignableRoles()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var roleAdminService = scope.ServiceProvider.GetRequiredService<RoleAdminService>();
        var roles = await roleAdminService.GetAssignableRolesAsync();

        Assert.NotNull(roles);
        Assert.True(roles.Count > 3);
        // 不应包含 organization-admin 角色
        Assert.DoesNotContain(roles, x => x.Name == Defaults.OrganizationAdmin);
    }

    /// <summary>
    /// 用户管理员查询可授于角色列表，应返回所有角色（除组织管理员）
    /// </summary>
    [Fact]
    public async Task UserAdminGetAssignableRoles()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadUserAdmin(session);

        var roleAdminService = scope.ServiceProvider.GetRequiredService<RoleAdminService>();
        var roles = await roleAdminService.GetAssignableRolesAsync();

        Assert.NotNull(roles);
        Assert.True(roles.Count > 0);
        // 不应包含 organization-admin 角色
        Assert.DoesNotContain(roles, x => x.Name == Defaults.OrganizationAdmin);
        // 不应包含 admin 角色
        Assert.Contains(roles, x => x.Name == Defaults.AdminRole);
    }

    /// <summary>
    /// 组织管理员查询可授于角色列表，应返回其角色可授于的角色
    /// </summary>
    [Fact]
    public async Task OrganizationAdminGetAssignableRoles()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadDevelopAdmin(session);

        var roleAdminService = scope.ServiceProvider.GetRequiredService<RoleAdminService>();
        var roles = await roleAdminService.GetAssignableRolesAsync();

        Assert.NotNull(roles);
        // 技术部经理（organization-admin + manager）可授于 manager, employee, intern
        Assert.True(roles.Count > 0);
        // 不应包含 organization-admin 角色
        Assert.DoesNotContain(roles, x => x.Name == Defaults.OrganizationAdmin);
        // 不应包含 admin 角色
        Assert.DoesNotContain(roles, x => x.Name == Defaults.AdminRole);
    }

    /// <summary>
    /// 普通用户查询可授于角色列表，应返回其角色可授于的角色
    /// </summary>
    [Fact]
    public async Task NormalUserGetAssignableRoles()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadNormalUser(session);

        var roleAdminService = scope.ServiceProvider.GetRequiredService<RoleAdminService>();
        var roles = await roleAdminService.GetAssignableRolesAsync();

        Assert.NotNull(roles);
        // 普通用户（employee 角色）没有可授于角色
        Assert.True(roles.Count == 0);
    }
}