using Microsoft.Extensions.DependencyInjection;
using WildGoose.Application.Organization.Admin.V10;
using WildGoose.Application.Organization.Admin.V10.Queries;
using WildGoose.Domain;
using Xunit;

namespace WildGoose.Tests;

[Collection("WebApplication collection")]
public class OrganizationAdminServiceTests(WebApplicationFactoryFixture fixture) : BaseTests
{
    /// <summary>
    /// 超管可以查看所有机构
    /// </summary>
    [Fact]
    public async Task SuperAdminGetRootSubList()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadAdmin(session);
        var organizationService = scope.ServiceProvider.GetRequiredService<OrganizationAdminService>();
        var list = await organizationService.GetSubListAsync(new GetSubListQuery());
        Assert.NotNull(list);
        Assert.Equal(3, list.Count);
        Assert.Equal("江苏省", list[0].Name);
        Assert.Equal("浙江省", list[1].Name);
        Assert.Equal("山东省", list[2].Name);
    }

    [Fact]
    public async Task SuperAdminGetSubList()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var organizationService = scope.ServiceProvider.GetRequiredService<OrganizationAdminService>();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadAdmin(session);
        var list = await organizationService.GetSubListAsync(new GetSubListQuery
        {
            ParentId = "65fd287cac42f1a071e1ed8b"
        });

        Assert.NotNull(list);
        Assert.Equal(4, list.Count);
        Assert.Equal("南京市", list[0].Name);
        Assert.Equal("无锡市", list[1].Name);
        Assert.Equal("苏州市", list[2].Name);
        Assert.Equal("连云港市", list[3].Name);
    }

    /// <summary>
    /// 机构管理员，使用管理接口，只能查看自身管理的机构
    /// </summary>
    [Fact]
    public async Task OrganizationAdminGetMyList()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadOrganizationAdmin(session);
        var organizationService = scope.ServiceProvider.GetRequiredService<OrganizationAdminService>();
        var list = await organizationService.GetSubListAsync(new GetSubListQuery());
        Assert.NotNull(list);
        Assert.Equal(2, list.Count);
        Assert.Equal("南京市", list[0].Name);
        Assert.Equal("无锡市", list[1].Name);
    }

    [Fact]
    public async Task OrganizationAdminGetSubList()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadOrganizationAdmin(session);
        var organizationService = scope.ServiceProvider.GetRequiredService<OrganizationAdminService>();
        var list = await organizationService.GetSubListAsync(new GetSubListQuery
        {
            // 南京 65fd28c6ac42f1a071e1ed8c
            // 67e4a99e370fa2bc6de04fc1
            ParentId = "65fd28c6ac42f1a071e1ed8c"
        });
        Assert.NotNull(list);
        Assert.Equal(1, list.Count);
        Assert.Equal("中山陵园管理局", list[0].Name);

        list = await organizationService.GetSubListAsync(new GetSubListQuery
        {
            // 江苏
            ParentId = "65fd287cac42f1a071e1ed8b"
        });
        Assert.NotNull(list);
        Assert.Equal(2, list.Count);
        Assert.Equal("南京市", list[0].Name);
        Assert.Equal("无锡市", list[1].Name);
    }

    /// <summary>
    /// 不在管理范围的机构查询不到数据
    /// </summary>
    [Fact]
    public async Task NonOrganizationAdminGetSubList()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var organizationService = scope.ServiceProvider.GetRequiredService<OrganizationAdminService>();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadOrganizationAdmin(session);
        var list = await organizationService.GetSubListAsync(new GetSubListQuery
        {
            // 连云港
            ParentId = "6625dac5933071f3b0ca1f29"
        });

        Assert.NotNull(list);
        Assert.Equal(0, list.Count);
    }

    [Fact]
    public async Task OrganizationAdminHasPermissionAsync()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var organizationService = scope.ServiceProvider.GetRequiredService<OrganizationAdminService>();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadOrganizationAdmin(session);
        // 浙江
        var r1 = await organizationService.HasOrganizationPermissionAsync("658996e69220b7372eb6489a");
        Assert.Equal(false, r1);
        // 江苏
        var r2 = await organizationService.HasOrganizationPermissionAsync("65fd287cac42f1a071e1ed8b");
        Assert.Equal(false, r2);
        // 南京
        var r3 = await organizationService.HasOrganizationPermissionAsync("65fd28c6ac42f1a071e1ed8c");
        Assert.Equal(true, r3);
    }

    [Fact]
    public async Task NormalUserHasPermissionAsync()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var organizationService = scope.ServiceProvider.GetRequiredService<OrganizationAdminService>();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadNormalUser(session);
        // 浙江
        var r1 = await organizationService.HasOrganizationPermissionAsync("658996e69220b7372eb6489a");
        Assert.Equal(false, r1);
        // 江苏
        var r2 = await organizationService.HasOrganizationPermissionAsync("65fd287cac42f1a071e1ed8b");
        Assert.Equal(false, r2);
        // 南京
        var r3 = await organizationService.HasOrganizationPermissionAsync("65fd28c6ac42f1a071e1ed8c");
        Assert.Equal(false, r3);
    }

    [Fact]
    public async Task NormalUserCheckUserPermission()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var organizationService = scope.ServiceProvider.GetRequiredService<OrganizationAdminService>();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadNormalUser(session);

        var exception = await Assert.ThrowsAsync<WildGooseFriendlyException>(async () =>
        {
            await organizationService.CheckUserPermissionAsync(session.UserId);
        });
        Assert.Equal("权限不足", exception.Message);
    }

    [Fact]
    public async Task SuperAdminCheckUserPermission()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var organizationService = scope.ServiceProvider.GetRequiredService<OrganizationAdminService>();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadAdmin(session);

        await organizationService.CheckUserPermissionAsync(session.UserId);
    }

    [Fact]
    public async Task OrganizationAdminCheckUserPermission()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var organizationService = scope.ServiceProvider.GetRequiredService<OrganizationAdminService>();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadOrganizationAdmin(session);

        // 自身
        await organizationService.CheckUserPermissionAsync("68540d88ed70c9c6b320673d");
        // 同级有权限的人
        await organizationService.CheckUserPermissionAsync("65fd29b7ac42f1a071e1ed8d");
        // 下级有权限的人
        await organizationService.CheckUserPermissionAsync("66a1e82dbb6e9bf87bb3dc44");

        var exception = await Assert.ThrowsAsync<WildGooseFriendlyException>(async () =>
        {
            await organizationService.CheckUserPermissionAsync("6625dfe7933071f3b0ca1f2f");
        });
        Assert.Equal("权限不足", exception.Message);
    }
}