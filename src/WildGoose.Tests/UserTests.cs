using Identity.Sm;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WildGoose.Application.User;
using WildGoose.Domain;
using WildGoose.Domain.Entity;
using WildGoose.Infrastructure;
using Xunit;

namespace WildGoose.Tests;

[Collection("WebApplication collection")]
public class UserTests(WebApplicationFactoryFixture fixture) : BaseTests
{
    /// <summary>
    /// 国密密码功能测试
    /// </summary>
    [Fact]
    public async Task RestPasswordAndVerify()
    {
        Environment.SetEnvironmentVariable("ENABLE_SM3_PASSWORD_HASHER", "true");
        var scope = fixture.Instance.Services.CreateScope();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        if (!passwordHasher.GetType().FullName!.Contains("Sm3PasswordHasher"))
        {
            throw new ArgumentException("未能启用国密hash");
        }
        var dbContext = scope.ServiceProvider.GetRequiredService<WildGooseDbContext>();
        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        LoadAdmin(session);
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var userName = Guid.NewGuid().ToString("N");
        var user = new User()
        {
            UserName = userName,
        };
        var createUserResult = await userManager.CreateAsync(user);
        if (createUserResult.Errors.Any())
        {
            throw new ArgumentException("创建随机用户失败");
        }

        var password = Guid.NewGuid().ToString("N");
        var extension = await dbContext.Set<UserExtension>()
            .FirstOrDefaultAsync(x => x.Id == user.Id);
        if (extension == null)
        {
            extension = new UserExtension { Id = user.Id };
            Utils.SetPasswordInfo(extension, password);
            await dbContext.AddAsync(extension);
        }
        else
        {
            Utils.SetPasswordInfo(extension, password);
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        await userManager.ResetPasswordAsync(user, token, password);
        // 修改密码后验证密码
        user = await userManager.FindByIdAsync(user.Id);
        Assert.Equal(PasswordVerificationResult.Success,
            passwordHasher.VerifyHashedPassword(user!, user!.PasswordHash!, password));
        await transaction.RollbackAsync();
    }
}