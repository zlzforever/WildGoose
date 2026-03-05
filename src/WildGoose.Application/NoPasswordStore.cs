using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using WildGoose.Infrastructure;

namespace WildGoose.Application;

public class NoPasswordStore(WildGooseDbContext context, IdentityErrorDescriber describer = null)
    : UserStore<Domain.Entity.User, Domain.Entity.Role, WildGooseDbContext>(context, describer)
{
    public override Task SetPasswordHashAsync(Domain.Entity.User user, string passwordHash, CancellationToken _)
    {
        // 不存密码
        return Task.CompletedTask;
    }

    public override Task<string> GetPasswordHashAsync(Domain.Entity.User user, CancellationToken _)
    {
        return Task.FromResult(string.Empty);
    }

    public override Task<bool> HasPasswordAsync(Domain.Entity.User user, CancellationToken _)
    {
        // ✅ 核心：告诉系统：用户没有密码
        return Task.FromResult(false);
    }
}