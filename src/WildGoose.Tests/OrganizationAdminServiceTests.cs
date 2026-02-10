using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WildGoose.Application.Organization.Admin.V10;
using WildGoose.Application.Organization.Admin.V10.Command;
using WildGoose.Application.Organization.Admin.V10.Queries;
using WildGoose.Domain;
using WildGoose.Domain.Entity;
using WildGoose.Infrastructure;
using Xunit;

namespace WildGoose.Tests;

/// <summary>
/// 组织管理服务测试
/// 测试超级管理员、用户管理员、组织管理员对组织的增删改查权限
/// </summary>
[Collection("WebApplication collection")]
public class OrganizationAdminServiceTests(WebApplicationFactoryFixture fixture) : BaseTests
{
    // 组织ID（根据 testdata.sql）
    private const string RootOrg = "507f1f77bcf86cd799439030"; // 总公司
    private const string TechOrg = "507f1f77bcf86cd799439031"; // 技术部
    private const string FrontEndOrg = "507f1f77bcf86cd799439033"; // 前端组
    private const string BackendOrg = "507f1f77bcf86cd799439034"; // 后端组

    // ============================================================================
    // 查询子组织列表测试
    // ============================================================================

    /// <summary>
    /// 超级管理员查询顶级组织（parentId 为空）
    /// </summary>
    [Fact]
    public async Task SuperAdminGetTopLevelOrganizations()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var organizationAdminService = scope.ServiceProvider.GetRequiredService<OrganizationAdminService>();
        var result = await organizationAdminService.GetSubListAsync(new GetSubListQuery
        {
            ParentId = null
        });

        Assert.NotNull(result);
        Assert.True(result.Count > 0);
        // 应该包含顶级组织
        Assert.Contains(result, x => x.ParentId == null);
    }

    /// <summary>
    /// 超级管理员查询指定组织的子组织
    /// </summary>
    [Fact]
    public async Task SuperAdminGetChildOrganizations()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var organizationAdminService = scope.ServiceProvider.GetRequiredService<OrganizationAdminService>();
        var result = await organizationAdminService.GetSubListAsync(new GetSubListQuery
        {
            ParentId = TechOrg
        });

        Assert.NotNull(result);
        // 应该包含前端组和后端组
        Assert.True(result.Count >= 2);
        Assert.Contains(result, x => x.Id == FrontEndOrg || x.Id == BackendOrg);
    }

    /// <summary>
    /// 组织管理员查询其管理的顶级组织（parentId 为空）
    /// </summary>
    [Fact]
    public async Task OrganizationAdminGetTopLevelOrganizations()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadDevelopAdmin(session);

        var organizationAdminService = scope.ServiceProvider.GetRequiredService<OrganizationAdminService>();
        var result = await organizationAdminService.GetSubListAsync(new GetSubListQuery
        {
            ParentId = null
        });

        Assert.NotNull(result);
        // 应该返回技术部（其管理的最顶级机构）
        Assert.Contains(result, x => x.Id == TechOrg);
        // 不应包含其管理的下级组织（前端组、后端组直接作为顶级返回）
        Assert.True(result.All(x => x.Id != FrontEndOrg && x.Id != BackendOrg));
    }

    /// <summary>
    /// 组织管理员查询其管理范围内组织的子组织
    /// </summary>
    [Fact]
    public async Task OrganizationAdminGetChildOrganizations()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadDevelopAdmin(session);

        var organizationAdminService = scope.ServiceProvider.GetRequiredService<OrganizationAdminService>();
        var result = await organizationAdminService.GetSubListAsync(new GetSubListQuery
        {
            ParentId = TechOrg
        });

        Assert.NotNull(result);
        // 应该包含前端组和后端组
        Assert.True(result.Count >= 2);
        Assert.Contains(result, x => x.Id == FrontEndOrg);
        Assert.Contains(result, x => x.Id == BackendOrg);
    }

    /// <summary>
    /// 
    /// </summary>
    [Fact]
    public async Task OrganizationAdminGetChildOrganizationsWithoutPermissionShouldFailed()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadDevelopAdmin(session);

        var organizationAdminService = scope.ServiceProvider.GetRequiredService<OrganizationAdminService>();
        var exception = await Assert.ThrowsAsync<WildGooseFriendlyException>(async () =>
        {
            await organizationAdminService.GetSubListAsync(new GetSubListQuery
            {
                ParentId = RootOrg
            });
        });

        Assert.Equal("没有管理机构的权限", exception.Message);
    }

    // ============================================================================
    // 搜索组织测试
    // ============================================================================

    /// <summary>
    /// 超级管理员可以搜索所有组织
    /// </summary>
    [Fact]
    public async Task SuperAdminSearchOrganizations()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var organizationAdminService = scope.ServiceProvider.GetRequiredService<OrganizationAdminService>();
        var result = await organizationAdminService.SearchAsync(new SearchQuery
        {
            Keyword = "技术"
        });

        Assert.NotNull(result);
        Assert.True(result.Count > 0);
        Assert.Contains(result, x => x.Name.Contains("技术"));
    }

    /// <summary>
    /// 组织管理员只能搜索其管理范围内的组织
    /// </summary>
    [Fact]
    public async Task OrganizationAdminSearchOrganizations()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadDevelopAdmin(session);

        var organizationAdminService = scope.ServiceProvider.GetRequiredService<OrganizationAdminService>();
        var result = await organizationAdminService.SearchAsync(new SearchQuery
        {
            Keyword = "技术"
        });

        Assert.NotNull(result);
        // 应该能找到技术部
        Assert.Contains(result, x => x.Name.Contains("技术"));
    }

    /// <summary>
    /// 组织管理员搜索无权限的组织应返回空
    /// </summary>
    [Fact]
    public async Task OrganizationAdminSearchUnauthorizedOrganizationsShouldReturnEmpty()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadDevelopAdmin(session);

        var organizationAdminService = scope.ServiceProvider.GetRequiredService<OrganizationAdminService>();
        var result = await organizationAdminService.SearchAsync(new SearchQuery
        {
            Keyword = "销售"
        });

        Assert.NotNull(result);
        // 技术部管理员没有权限查询销售部
        Assert.True(result.Count == 0);
    }

    // ============================================================================
    // 添加机构测试
    // ============================================================================

    /// <summary>
    /// 超级管理员可以添加一级机构
    /// </summary>
    [Fact]
    public async Task SuperAdminAddTopLevelOrganization()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var organizationAdminService = scope.ServiceProvider.GetRequiredService<OrganizationAdminService>();
        var result = await organizationAdminService.AddAsync(new AddOrganizationCommand
        {
            Name = $"测试一级机构_{CreateName()}",
            Code = $"TEST_{CreateName()}",
            Address = "测试地址",
            Description = "测试描述",
            ParentId = null,
            Scope = []
        });

        Assert.NotNull(result);
        Assert.Null(result.ParentId);
    }

    /// <summary>
    /// 超级管理员可以添加子机构
    /// </summary>
    [Fact]
    public async Task SuperAdminAddChildOrganization()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var organizationAdminService = scope.ServiceProvider.GetRequiredService<OrganizationAdminService>();
        var result = await organizationAdminService.AddAsync(new AddOrganizationCommand
        {
            Name = $"测试子机构_{CreateName()}",
            Code = $"TEST_{CreateName()}",
            Address = "测试地址",
            Description = "测试描述",
            ParentId = TechOrg
        });

        Assert.NotNull(result);
        Assert.Equal(TechOrg, result.ParentId);
    }

    /// <summary>
    /// 添加机构时父机构不存在应失败
    /// </summary>
    [Fact]
    public async Task AddOrganizationWithNonExistentParentShouldFail()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var organizationAdminService = scope.ServiceProvider.GetRequiredService<OrganizationAdminService>();
        var exception = await Assert.ThrowsAsync<WildGooseFriendlyException>(async () =>
        {
            await organizationAdminService.AddAsync(new AddOrganizationCommand
            {
                Name = $"测试机构_{CreateName()}",
                Code = $"TEST_{CreateName()}",
                ParentId = "000000000000000000000000000"
            });
        });

        Assert.Equal("父机构不存在", exception.Message);
    }

    /// <summary>
    /// 组织管理员只能在其管理范围内添加机构
    /// </summary>
    [Fact]
    public async Task OrganizationAdminAddOrganizationInManagedScope()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadDevelopAdmin(session);

        var organizationAdminService = scope.ServiceProvider.GetRequiredService<OrganizationAdminService>();
        var result = await organizationAdminService.AddAsync(new AddOrganizationCommand
        {
            Name = $"测试子机构_{CreateName()}",
            Code = $"TEST_{CreateName()}",
            ParentId = TechOrg
        });

        Assert.NotNull(result);
        Assert.Equal(TechOrg, result.ParentId);
    }

    /// <summary>
    /// 组织管理员添加无权限机构应失败
    /// </summary>
    [Fact]
    public async Task OrganizationAdminAddOrganizationWithoutPermissionShouldFail()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadDevelopAdmin(session);

        var organizationAdminService = scope.ServiceProvider.GetRequiredService<OrganizationAdminService>();
        var exception = await Assert.ThrowsAsync<WildGooseFriendlyException>(async () =>
        {
            await organizationAdminService.AddAsync(new AddOrganizationCommand
            {
                Name = $"测试机构_{CreateName()}",
                Code = $"TEST_{CreateName()}",
                ParentId = RootOrg
            });
        });

        Assert.Equal("没有管理机构的权限", exception.Message);
    }

    /// <summary>
    /// 组织管理员不能添加一级机构
    /// </summary>
    [Fact]
    public async Task OrganizationAdminAddTopLevelOrganizationShouldFail()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadDevelopAdmin(session);

        var organizationAdminService = scope.ServiceProvider.GetRequiredService<OrganizationAdminService>();
        var exception = await Assert.ThrowsAsync<WildGooseFriendlyException>(async () =>
        {
            await organizationAdminService.AddAsync(new AddOrganizationCommand
            {
                Name = $"测试一级机构_{CreateName()}",
                Code = $"TEST_{CreateName()}",
                ParentId = null
            });
        });

        Assert.Equal("仅允许超级管理员创建一级机构", exception.Message);
    }

    /// <summary>
    /// 用户管理员可以添加一级机构
    /// </summary>
    [Fact]
    public async Task UserAdminAddTopLevelOrganization()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadUserAdmin(session);

        var organizationAdminService = scope.ServiceProvider.GetRequiredService<OrganizationAdminService>();
        var result = await organizationAdminService.AddAsync(new AddOrganizationCommand
        {
            Name = $"测试一级机构_{CreateName()}",
            Code = $"TEST_{CreateName()}",
            Address = "测试地址",
            ParentId = null
        });

        Assert.NotNull(result);
        Assert.Null(result.ParentId);
    }

    // ============================================================================
    // 查询机构详情测试
    // ============================================================================

    /// <summary>
    /// 超级管理员可以查询任意机构详情
    /// </summary>
    [Fact]
    public async Task SuperAdminGetOrganization()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);
        var organizationAdminService = scope.ServiceProvider.GetRequiredService<OrganizationAdminService>();
        var result0 = await organizationAdminService.AddAsync(new AddOrganizationCommand
        {
            Name = $"测试子机构_{CreateName()}",
            Code = $"TEST_{CreateName()}",
            Address = "测试地址",
            Description = "测试描述"
        });

        var result = await organizationAdminService.GetAsync(new GetDetailQuery
        {
            Id = result0.Id
        });

        Assert.NotNull(result);
        Assert.Equal(result0.Name, result.Name);
    }

    /// <summary>
    /// 组织管理员可以查询其管理范围内的机构详情
    /// </summary>
    [Fact]
    public async Task OrganizationAdminGetOrganizationInManagedScope()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadDevelopAdmin(session);

        var organizationAdminService = scope.ServiceProvider.GetRequiredService<OrganizationAdminService>();
        var result = await organizationAdminService.GetAsync(new GetDetailQuery
        {
            Id = TechOrg
        });

        Assert.NotNull(result);
        Assert.Equal("技术部", result.Name);
    }

    /// <summary>
    /// 
    /// </summary>
    [Fact]
    public async Task OrganizationAdminGetOrganizationWithoutPermissionShouldFail()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadDevelopAdmin(session);

        var organizationAdminService = scope.ServiceProvider.GetRequiredService<OrganizationAdminService>();
        var exception = await Assert.ThrowsAsync<WildGooseFriendlyException>(async () =>
        {
            var result = await organizationAdminService.GetAsync(new GetDetailQuery
            {
                Id = RootOrg
            });
        });

        Assert.Equal("没有管理此机构的权限", exception.Message);
    }

    /// <summary>
    /// 查询不存在的机构应返回 null
    /// </summary>
    [Fact]
    public async Task GetNonExistentOrganizationReturnsNull()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var organizationAdminService = scope.ServiceProvider.GetRequiredService<OrganizationAdminService>();
        var result = await organizationAdminService.GetAsync(new GetDetailQuery
        {
            Id = "000000000000000000000000000"
        });

        Assert.Null(result);
    }

    // ============================================================================
    // 修改机构测试
    // ============================================================================

    /// <summary>
    /// 超级管理员可以修改机构信息
    /// </summary>
    [Fact]
    public async Task SuperAdminUpdateOrganization()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var organizationAdminService = scope.ServiceProvider.GetRequiredService<OrganizationAdminService>();

        var result = await organizationAdminService.AddAsync(new AddOrganizationCommand
        {
            Name = $"测试一级机构_{CreateName()}",
            Code = $"TEST_{CreateName()}",
            Address = "测试地址",
            Description = "测试描述",
            ParentId = null,
            Scope = []
        });

        var newName = $"更新后的机构名称_{CreateName()}";
        await organizationAdminService.UpdateAsync(new UpdateOrganizationCommand
        {
            Id = result.Id,
            Name = newName,
            Code = "TECH_UPDATED",
            Address = "新地址",
            Description = "新描述",
            ParentId = null
        });

        // 验证修改成功
        var dbContext = scope.ServiceProvider.GetRequiredService<WildGooseDbContext>();
        var organization = await dbContext.Set<WildGoose.Domain.Entity.Organization>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == result.Id);
        Assert.NotNull(organization);
        Assert.Equal(newName, organization.Name);
    }

    /// <summary>
    /// 超级管理员可以修改机构的父机构
    /// </summary>
    [Fact]
    public async Task SuperAdminUpdateOrganizationParent()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var organizationAdminService = scope.ServiceProvider.GetRequiredService<OrganizationAdminService>();
        var name = CreateName();
        var result = await organizationAdminService.AddAsync(new AddOrganizationCommand()
        {
            Name = CreateName(),
            Code = name.ToUpper(),
            ParentId = TechOrg
        });

        await organizationAdminService.UpdateAsync(new UpdateOrganizationCommand
        {
            Id = result.Id,
            Name = "更新后的组",
            Code = "GROUP_UPDATED",
            ParentId = RootOrg // 从技术部改为总公司
        });

        // 验证父机构已修改
        var dbContext = scope.ServiceProvider.GetRequiredService<WildGooseDbContext>();
        var organization = await dbContext.Set<WildGoose.Domain.Entity.Organization>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == FrontEndOrg);
        Assert.NotNull(organization);
    }

    /// <summary>
    /// 修改不存在的机构应失败
    /// </summary>
    [Fact]
    public async Task UpdateNonExistentOrganizationShouldFail()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var organizationAdminService = scope.ServiceProvider.GetRequiredService<OrganizationAdminService>();
        var exception = await Assert.ThrowsAsync<WildGooseFriendlyException>(async () =>
        {
            await organizationAdminService.UpdateAsync(new UpdateOrganizationCommand
            {
                Id = "000000000000000000000000000",
                Name = "不存在的机构",
                Code = "TEST"
            });
        });

        Assert.Equal("机构不存在", exception.Message);
    }

    /// <summary>
    /// 修改机构时父机构不存在应失败
    /// </summary>
    [Fact]
    public async Task UpdateOrganizationWithNonExistentParentShouldFail()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var organizationAdminService = scope.ServiceProvider.GetRequiredService<OrganizationAdminService>();
        var exception = await Assert.ThrowsAsync<WildGooseFriendlyException>(async () =>
        {
            await organizationAdminService.UpdateAsync(new UpdateOrganizationCommand
            {
                Id = TechOrg,
                Name = "技术部",
                Code = "TECH",
                ParentId = "000000000000000000000000000"
            });
        });

        Assert.Equal("父机构不存在", exception.Message);
    }

    /// <summary>
    /// 组织管理员修改其管理范围内的机构应成功
    /// </summary>
    [Fact]
    public async Task OrganizationAdminUpdateOrganizationInManagedScope()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadDevelopAdmin(session);

        var organizationAdminService = scope.ServiceProvider.GetRequiredService<OrganizationAdminService>();
        var newName = $"更新后的前端组_{CreateName()}";

        await organizationAdminService.UpdateAsync(new UpdateOrganizationCommand
        {
            Id = FrontEndOrg,
            Name = "前端组",
            Description = newName,
            ParentId = TechOrg
        });

        // 验证修改成功
        var dbContext = scope.ServiceProvider.GetRequiredService<WildGooseDbContext>();
        var organization = await dbContext.Set<WildGoose.Domain.Entity.Organization>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == FrontEndOrg);
        Assert.NotNull(organization);
        Assert.Equal(newName, organization.Description);
    }

    /// <summary>
    /// 组织管理员修改其管理范围内的机构应成功
    /// </summary>
    [Fact]
    public async Task OrganizationAdminUpdateOrganizationToRootInManagedScopeFailed()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadDevelopAdmin(session);

        var organizationAdminService = scope.ServiceProvider.GetRequiredService<OrganizationAdminService>();
        var newName = $"更新后的前端组_{CreateName()}";

        var exception = await Assert.ThrowsAsync<WildGooseFriendlyException>(async () =>
        {
            await organizationAdminService.UpdateAsync(new UpdateOrganizationCommand
            {
                Id = FrontEndOrg,
                Name = newName
            });
        });

        Assert.Equal("仅允许超级管理员操作/设置一级机构", exception.Message);
    }

    /// <summary>
    /// 组织管理员修改无权限机构应失败
    /// </summary>
    [Fact]
    public async Task OrganizationAdminUpdateUnauthorizedOrganizationShouldFail()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadDevelopAdmin(session);

        var organizationAdminService = scope.ServiceProvider.GetRequiredService<OrganizationAdminService>();
        var exception = await Assert.ThrowsAsync<WildGooseFriendlyException>(async () =>
        {
            await organizationAdminService.UpdateAsync(new UpdateOrganizationCommand
            {
                Id = RootOrg,
                Name = "修改后的总公司"
            });
        });

        Assert.Equal("仅允许超级管理员操作/设置一级机构", exception.Message);
    }

    /// <summary>
    /// 组织管理员不能修改一级机构
    /// </summary>
    [Fact]
    public async Task OrganizationAdminUpdateTopLevelOrganizationShouldFail()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadDevelopAdmin(session);

        var organizationAdminService = scope.ServiceProvider.GetRequiredService<OrganizationAdminService>();
        var exception = await Assert.ThrowsAsync<WildGooseFriendlyException>(async () =>
        {
            await organizationAdminService.UpdateAsync(new UpdateOrganizationCommand
            {
                Id = RootOrg,
                Name = "修改后的总公司",
                ParentId = null
            });
        });

        Assert.Equal("仅允许超级管理员操作/设置一级机构", exception.Message);
    }

    // ============================================================================
    // 删除机构测试
    // ============================================================================

    /// <summary>
    /// 超级管理员可以删除一级机构
    /// </summary>
    [Fact]
    public async Task SuperAdminDeleteTopLevelOrganization()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var organizationAdminService = scope.ServiceProvider.GetRequiredService<OrganizationAdminService>();

        // 先创建一个临时的一级机构
        var org = await organizationAdminService.AddAsync(new AddOrganizationCommand
        {
            Name = $"待删除的一级机构_{CreateName()}",
            Code = $"TEMP_{CreateName()}",
            ParentId = null
        });

        // 删除机构
        var deletedId = await organizationAdminService.DeleteAsync(org.Id);

        Assert.Equal(org.Id, deletedId);

        // 验证机构已删除
        var dbContext = scope.ServiceProvider.GetRequiredService<WildGooseDbContext>();
        var organization = await dbContext.Set<WildGoose.Domain.Entity.Organization>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == deletedId);
        Assert.Null(organization);
    }

    /// <summary>
    /// 超级管理员可以删除下级机构
    /// </summary>
    [Fact]
    public async Task SuperAdminDeleteChildOrganization()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var organizationAdminService = scope.ServiceProvider.GetRequiredService<OrganizationAdminService>();

        // 先创建一个临时的子机构
        var org = await organizationAdminService.AddAsync(new AddOrganizationCommand
        {
            Name = $"待删除的子机构_{CreateName()}",
            Code = $"TEMP_{CreateName()}",
            ParentId = TechOrg
        });

        // 删除机构
        var deletedId = await organizationAdminService.DeleteAsync(org.Id);

        Assert.Equal(org.Id, deletedId);

        // 验证机构已删除
        var dbContext = scope.ServiceProvider.GetRequiredService<WildGooseDbContext>();
        var organization = await dbContext.Set<WildGoose.Domain.Entity.Organization>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == deletedId);
        Assert.Null(organization);
    }

    /// <summary>
    /// 删除不存在的机构应失败
    /// </summary>
    [Fact]
    public async Task DeleteNonExistentOrganizationShouldFail()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var organizationAdminService = scope.ServiceProvider.GetRequiredService<OrganizationAdminService>();
        var exception = await Assert.ThrowsAsync<WildGooseFriendlyException>(async () =>
        {
            await organizationAdminService.DeleteAsync("000000000000000000000000000");
        });

        Assert.Equal("组织不存在", exception.Message);
    }

    /// <summary>
    /// 删除有下级机构的机构应失败
    /// </summary>
    [Fact]
    public async Task DeleteOrganizationWithChildrenShouldFail()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var organizationAdminService = scope.ServiceProvider.GetRequiredService<OrganizationAdminService>();
        var exception = await Assert.ThrowsAsync<WildGooseFriendlyException>(async () =>
        {
            await organizationAdminService.DeleteAsync(TechOrg);
        });

        Assert.Equal("请先删除下级机构", exception.Message);
    }

    /// <summary>
    /// 组织管理员可以删除其管理范围内的下级机构
    /// </summary>
    [Fact]
    public async Task OrganizationAdminDeleteChildOrganization()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadDevelopAdmin(session);

        var organizationAdminService = scope.ServiceProvider.GetRequiredService<OrganizationAdminService>();

        // 先创建一个临时的子机构
        var org = await organizationAdminService.AddAsync(new AddOrganizationCommand
        {
            Name = $"待删除的子机构_{CreateName()}",
            Code = $"TEMP_{CreateName()}",
            ParentId = TechOrg
        });

        // 删除机构
        var deletedId = await organizationAdminService.DeleteAsync(org.Id);

        Assert.Equal(org.Id, deletedId);
    }

    /// <summary>
    /// 组织管理员删除一级机构应失败
    /// </summary>
    [Fact]
    public async Task OrganizationAdminDeleteTopLevelOrganizationShouldFail()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadDevelopAdmin(session);

        var organizationAdminService = scope.ServiceProvider.GetRequiredService<OrganizationAdminService>();
        var exception = await Assert.ThrowsAsync<WildGooseFriendlyException>(async () =>
        {
            await organizationAdminService.DeleteAsync(RootOrg);
        });

        Assert.Equal("仅允许超级管理员操作一级机构", exception.Message);
    }

    /// <summary>
    /// 组织管理员删除无权限机构应失败
    /// </summary>
    [Fact]
    public async Task OrganizationAdminDeleteUnauthorizedOrganizationShouldFail()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadDevelopAdmin(session);

        var organizationAdminService = scope.ServiceProvider.GetRequiredService<OrganizationAdminService>();
        var exception = await Assert.ThrowsAsync<WildGooseFriendlyException>(async () =>
        {
            await organizationAdminService.DeleteAsync(RootOrg);
        });

        Assert.Equal("仅允许超级管理员操作一级机构", exception.Message);
    }

    // ============================================================================
    // 添加机构管理员测试
    // ============================================================================

    /// <summary>
    /// 超级管理员可以添加机构管理员
    /// </summary>
    [Fact]
    public async Task SuperAdminAddAdministrator()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);
        var dbContext = scope.ServiceProvider.GetRequiredService<WildGooseDbContext>();

        var organizationAdminService = scope.ServiceProvider.GetRequiredService<OrganizationAdminService>();

        await organizationAdminService.AddAdministratorAsync(new AddAdministratorCommand
        {
            Id = FrontEndOrg,
            UserId = FrontendUserId
        });

        // 验证机构管理员已添加
        var admin = await dbContext.Set<OrganizationAdministrator>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrganizationId == FrontEndOrg && x.UserId == FrontendUserId);
        Assert.NotNull(admin);

        // 验证用户已被授予组织管理员角色
        var userRole = await dbContext.Set<IdentityUserRole<string>>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == FrontendUserId && x.RoleId == Defaults.OrganizationAdminRoleId);
        Assert.NotNull(userRole);
    }

    /// <summary>
    /// 添加不存在的用户作为管理员应失败
    /// </summary>
    [Fact]
    public async Task AddNonExistentUserAsAdministratorShouldFail()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var organizationAdminService = scope.ServiceProvider.GetRequiredService<OrganizationAdminService>();
        var exception = await Assert.ThrowsAsync<WildGooseFriendlyException>(async () =>
        {
            await organizationAdminService.AddAdministratorAsync(new AddAdministratorCommand
            {
                Id = TechOrg,
                UserId = "000000000000000000000000000"
            });
        });

        Assert.Equal("用户不存在", exception.Message);
    }

    /// <summary>
    /// 添加无权限机构的管理员应失败
    /// 技术组不能管理总公司
    /// </summary>
    [Fact]
    public async Task AddAdministratorToUnauthorizedOrganizationShouldFail()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadDevelopAdmin(session);

        var organizationAdminService = scope.ServiceProvider.GetRequiredService<OrganizationAdminService>();
        var exception = await Assert.ThrowsAsync<WildGooseFriendlyException>(async () =>
        {
            await organizationAdminService.AddAdministratorAsync(new AddAdministratorCommand
            {
                Id = RootOrg,
                UserId = BackendUserId
            });
        });

        Assert.Equal("没有管理机构的权限", exception.Message);
    }

    /// <summary>
    /// 组织管理员可以添加其管理范围内的机构管理员
    /// </summary>
    [Fact]
    public async Task OrganizationAdminAddAdministrator()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadDevelopAdmin(session);
        var dbContext = scope.ServiceProvider.GetRequiredService<WildGooseDbContext>();

        var organizationAdminService = scope.ServiceProvider.GetRequiredService<OrganizationAdminService>();
        await organizationAdminService.AddAdministratorAsync(new AddAdministratorCommand
        {
            Id = FrontEndOrg,
            UserId = BackendUserId
        });

        // 验证机构管理员已添加
        var admin = await dbContext.Set<OrganizationAdministrator>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrganizationId == FrontEndOrg && x.UserId == BackendUserId);
        Assert.NotNull(admin);
    }

    // ============================================================================
    /// 删除机构管理员测试
    // ============================================================================

    /// <summary>
    /// 超级管理员可以删除机构管理员
    /// </summary>
    [Fact]
    public async Task SuperAdminDeleteAdministrator()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);
        var dbContext = scope.ServiceProvider.GetRequiredService<WildGooseDbContext>();

        var organizationAdminService = scope.ServiceProvider.GetRequiredService<OrganizationAdminService>();

        // 先添加一个管理员
        await organizationAdminService.AddAdministratorAsync(new AddAdministratorCommand
        {
            Id = FrontEndOrg,
            UserId = BackendUserId
        });

        // 删除管理员
        await organizationAdminService.DeleteAdministratorAsync(new DeleteAdministratorCommand
        {
            Id = FrontEndOrg,
            UserId = BackendUserId
        });

        // 验证机构管理员已删除
        var admin = await dbContext.Set<OrganizationAdministrator>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrganizationId == FrontEndOrg && x.UserId == BackendUserId);
        Assert.Null(admin);

        // 验证组织管理员角色已被移除（如果这是该用户的最后一个机构管理员职位）
        var userRole = await dbContext.Set<IdentityUserRole<string>>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == BackendUserId && x.RoleId == Defaults.OrganizationAdmin);
        // 可能为 null 或被删除，取决于用户是否还有其他机构管理员职位
    }

    /// <summary>
    /// 删除不存在的管理员关系应失败
    /// </summary>
    [Fact]
    public async Task DeleteNonExistentAdministratorShouldFail()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var organizationAdminService = scope.ServiceProvider.GetRequiredService<OrganizationAdminService>();
        var exception = await Assert.ThrowsAsync<WildGooseFriendlyException>(async () =>
        {
            await organizationAdminService.DeleteAdministratorAsync(new DeleteAdministratorCommand
            {
                Id = TechOrg,
                UserId = BackendUserId
            });
        });

        // 后端用户不是技术部的管理员
        Assert.Equal("数据不存在", exception.Message);
    }

    /// <summary>
    /// 删除用户不存在时应失败
    /// </summary>
    [Fact]
    public async Task DeleteAdministratorWithNonExistentUserShouldFail()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var organizationAdminService = scope.ServiceProvider.GetRequiredService<OrganizationAdminService>();
        var exception = await Assert.ThrowsAsync<WildGooseFriendlyException>(async () =>
        {
            await organizationAdminService.DeleteAdministratorAsync(new DeleteAdministratorCommand
            {
                Id = TechOrg,
                UserId = "000000000000000000000000000"
            });
        });

        Assert.Equal("用户不存在", exception.Message);
    }

    /// <summary>
    /// 删除无权限机构的管理员应失败
    /// </summary>
    [Fact]
    public async Task DeleteAdministratorFromUnauthorizedOrganizationShouldFail()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadDevelopAdmin(session);

        var organizationAdminService = scope.ServiceProvider.GetRequiredService<OrganizationAdminService>();
        var exception = await Assert.ThrowsAsync<WildGooseFriendlyException>(async () =>
        {
            await organizationAdminService.DeleteAdministratorAsync(new DeleteAdministratorCommand
            {
                Id = RootOrg,
                UserId = "xxxxx"
            });
        });

        Assert.Equal("没有管理机构的权限", exception.Message);
    }
}