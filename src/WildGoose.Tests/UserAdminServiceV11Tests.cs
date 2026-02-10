using Microsoft.Extensions.DependencyInjection;
using WildGoose.Application.User.Admin.V11;
using WildGoose.Application.User.Admin.V11.Command;
using WildGoose.Domain;
using Xunit;

namespace WildGoose.Tests;

[Collection("WebApplication collection")]
public class UserAdminServiceV11Tests(WebApplicationFactoryFixture fixture) : BaseTests
{
    [Fact]
    public async Task SuperAddUserFailed()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadSuperAdmin(session);

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();
        var exception = await Assert.ThrowsAsync<WildGooseFriendlyException>(async () =>
        {
            await userAdminService.AddAsync(new AddUserCommand()
            {
                UserName = CreateName()
            });
        });
        Assert.Equal("访问受限", exception.Message);
    }

    [Fact]
    public async Task OrganizationAdminAddUserFailed()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadDevelopAdmin(session);

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();
        var exception = await Assert.ThrowsAsync<WildGooseFriendlyException>(async () =>
        {
            await userAdminService.AddAsync(new AddUserCommand()
            {
                UserName = CreateName()
            });
        });
        Assert.Equal("访问受限", exception.Message);
    }

    [Fact]
    public async Task NormalUserAddUserFailed()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadNormalUser(session);

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();
        var exception = await Assert.ThrowsAsync<WildGooseFriendlyException>(async () =>
        {
            await userAdminService.AddAsync(new AddUserCommand()
            {
                UserName = CreateName()
            });
        });
        Assert.Equal("访问受限", exception.Message);
    }

    [Fact]
    public async Task UserAdminAddUserFailed()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadUserAdmin(session);

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();
        var exception = await Assert.ThrowsAsync<WildGooseFriendlyException>(async () =>
        {
            await userAdminService.AddAsync(new AddUserCommand()
            {
                UserName = CreateName()
            });
        });
        Assert.Equal("访问受限", exception.Message);
    }

    [Fact]
    public async Task InternalAddUserRoleAddUserSuccess()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadInternalAddUserRole(session);

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();
        var user = await userAdminService.AddAsync(new AddUserCommand
        {
            UserName = CreateName(),
            Password = "As@2028!bcd"
        });
        Assert.NotNull(user);
    }
    
    [Fact]
    public async Task InternalAddUserRoleAddUserWithoutPasswordSuccess()
    {
        var scope = fixture.Instance.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadInternalAddUserRole(session);

        var userAdminService = scope.ServiceProvider.GetRequiredService<UserAdminService>();
        var user = await userAdminService.AddAsync(new AddUserCommand
        {
            UserName = CreateName()
        });
        Assert.NotNull(user);
    }
}