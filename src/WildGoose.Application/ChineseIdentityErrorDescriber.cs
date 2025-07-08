using Microsoft.AspNetCore.Identity;

namespace WildGoose.Application;

public class ChineseIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DefaultError()
    {
        return new IdentityError { Code = nameof(DefaultError), Description = "未知错误" };
    }

    public override IdentityError ConcurrencyFailure()
    {
        return new IdentityError { Code = nameof(ConcurrencyFailure), Description = "并发错误， 数据已被修改" };
    }

    public override IdentityError PasswordMismatch()
    {
        return new IdentityError { Code = "Password", Description = "密码错误" };
    }

    public override IdentityError InvalidToken()
    {
        return new IdentityError { Code = nameof(InvalidToken), Description = "访问令牌不合法" };
    }

    public override IdentityError LoginAlreadyAssociated()
    {
        return new IdentityError { Code = nameof(LoginAlreadyAssociated), Description = "当前用户已经登录" };
    }

    public override IdentityError InvalidUserName(string userName)
    {
        return new IdentityError { Code = "UserName", Description = $"用户名 '{userName}' 不合法" };
    }

    public override IdentityError InvalidEmail(string email)
    {
        return new IdentityError { Code = "Email", Description = $"邮箱 '{email}' 格式错误" };
    }

    public override IdentityError DuplicateUserName(string userName)
    {
        return new IdentityError { Code = "UserName", Description = $"用户名 '{userName}' 已存在" };
    }

    public override IdentityError DuplicateEmail(string email)
    {
        return new IdentityError { Code = "Email", Description = $"邮箱 '{email}' 已经存在" };
    }

    public override IdentityError InvalidRoleName(string role)
    {
        return new IdentityError { Code = nameof(InvalidRoleName), Description = $"角色 '{role}' 名不合法" };
    }

    public override IdentityError DuplicateRoleName(string role)
    {
        return new IdentityError { Code = nameof(DuplicateRoleName), Description = $"角色名 '{role}' 已经存在" };
    }

    public override IdentityError UserAlreadyHasPassword()
    {
        return new IdentityError
            { Code = nameof(UserAlreadyHasPassword), Description = "用户已经设置了密码" };
    }

    public override IdentityError UserLockoutNotEnabled()
    {
        return new IdentityError
            { Code = nameof(UserLockoutNotEnabled), Description = "该用户不允许锁定" };
    }

    public override IdentityError UserAlreadyInRole(string role)
    {
        return new IdentityError { Code = nameof(UserAlreadyInRole), Description = $"用户已经添加角色 '{role}'." };
    }

    public override IdentityError UserNotInRole(string role)
    {
        return new IdentityError { Code = nameof(UserNotInRole), Description = $"用户未添加角色 '{role}'." };
    }

    public override IdentityError PasswordTooShort(int length)
    {
        return new IdentityError { Code = "Password", Description = $"密码至少 {length} 位" };
    }

    public override IdentityError PasswordRequiresNonAlphanumeric()
    {
        return new IdentityError { Code = "Password", Description = "密码必须至少有一个非字母数字字符." };
    }

    public override IdentityError PasswordRequiresDigit()
    {
        return new IdentityError { Code = "Password", Description = "密码至少有一个数字 ('0'-'9')." };
    }

    public override IdentityError PasswordRequiresLower()
    {
        return new IdentityError { Code = "Password", Description = "密码必须包含小写字母 ('a'-'z')." };
    }

    public override IdentityError PasswordRequiresUpper()
    {
        return new IdentityError { Code = "Password", Description = "密码必须包含大写字母 ('A'-'Z')." };
    }
}