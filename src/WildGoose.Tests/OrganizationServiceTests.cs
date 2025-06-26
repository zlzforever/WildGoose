using Microsoft.Extensions.DependencyInjection;
using WildGoose.Application.Organization.V10;
using WildGoose.Application.Organization.V10.Queries;
using WildGoose.Domain;
using Xunit;

namespace WildGoose.Tests;

/// <summary>
/// 通用机构接口
/// 任何登录的人都可以访问（超管除外）
/// </summary>
/// <param name="fixture"></param>
[Collection("WebApplication collection")]
public class OrganizationServiceTests(WebApplicationFactoryFixture fixture) : BaseTests
{
    /// <summary>
    /// 超管调用业务接口不返回数据
    /// </summary>
    [Fact]
    public async Task SuperAdminGetRootSubList()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadAdmin(session);
        var organizationService = scope.ServiceProvider.GetRequiredService<OrganizationService>();
        var list = await organizationService.GetSubListAsync(new GetSubListQuery());
        Assert.Equal(0, list.Count);
    }

    /// <summary>
    /// 超管调用业务接口不返回数据
    /// </summary>
    [Fact]
    public async Task SuperAdminGetSubList()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var organizationService = scope.ServiceProvider.GetRequiredService<OrganizationService>();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadAdmin(session);
        var list = await organizationService.GetSubListAsync(new GetSubListQuery
        {
            ParentId = "65fd287cac42f1a071e1ed8b"
        });
        Assert.Equal(0, list.Count);
    }

    /// <summary>
    /// 非超管用户查询根节点数据，返回根节点数据。
    /// </summary>
    [Fact]
    public async Task NormalUserGetRootSubList()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadNormalUser(session);
        var organizationService = scope.ServiceProvider.GetRequiredService<OrganizationService>();
        var list = await organizationService.GetSubListAsync(new GetSubListQuery());
        Assert.NotNull(list);
        Assert.Equal(3, list.Count);
        Assert.Equal("江苏省", list[0].Name);
        Assert.Equal("浙江省", list[1].Name);
        Assert.Equal("山东省", list[2].Name);
    }

    /// <summary>
    /// 非超管用户查询节点数据，返回节点数据。
    /// </summary>
    [Fact]
    public async Task NormalUserGetSubList()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadNormalUser(session);
        var organizationService = scope.ServiceProvider.GetRequiredService<OrganizationService>();
        var list = await organizationService.GetSubListAsync(new GetSubListQuery
        {
            // 江苏
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
    /// 非超管用户查询根节点数据，返回根节点数据。
    /// </summary>
    [Fact]
    public async Task OrganizationAdminGetRootSubList()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadOrganizationAdmin(session);
        var organizationService = scope.ServiceProvider.GetRequiredService<OrganizationService>();
        var list = await organizationService.GetSubListAsync(new GetSubListQuery());
        Assert.NotNull(list);
        Assert.Equal(3, list.Count);
        Assert.Equal("江苏省", list[0].Name);
        Assert.Equal("浙江省", list[1].Name);
        Assert.Equal("山东省", list[2].Name);
    }

    [Fact]
    public async Task OrganizationAdminGetSubList()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadOrganizationAdmin(session);
        var organizationService = scope.ServiceProvider.GetRequiredService<OrganizationService>();
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
    /// 查询登录用户所在的所有机构，如果存在上下级关系，则只返回其上级。因上下级可以一级级展开获取、查看。
    /// </summary>
    [Fact]
    public async Task OrganizationAdminGetMyRootSubList()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadOrganizationAdmin(session);
        var organizationService = scope.ServiceProvider.GetRequiredService<OrganizationService>();
        var list = await organizationService.GetSubListAsync(new GetSubListQuery
        {
            Type = "my"
        });

        Assert.NotNull(list);
        Assert.Equal(2, list.Count);
        Assert.Equal("南京市", list[0].Name);
        Assert.Equal("无锡市", list[1].Name);
    }
}