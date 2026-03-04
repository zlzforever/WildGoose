using Microsoft.AspNetCore.Identity;

namespace WildGoose.Application;

public class NoopPasswordValidator<TUser> : IPasswordValidator<TUser> where TUser : class
{
    public Task<IdentityResult> ValidateAsync(UserManager<TUser> manager, TUser user, string password)
    {
        // 永远返回成功 = 完全跳过密码验证
        return Task.FromResult(IdentityResult.Success);
    }
}