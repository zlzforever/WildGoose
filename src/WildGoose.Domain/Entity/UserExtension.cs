namespace WildGoose.Domain.Entity;

public class UserExtension
{
    public string Id { get; set; }

    /// <summary>
    /// 离职时间
    /// </summary>
    public DateTimeOffset? DepartureTime { get; set; }

    /// <summary>
    /// 工作职位
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// 密码是否包含数字
    /// </summary>
    public bool PasswordContainsDigit { get; set; }

    /// <summary>
    /// 密码是否包含小写
    /// </summary>
    public bool PasswordContainsLowercase { get; set; }

    /// <summary>
    /// 密码是否包含大写
    /// </summary>
    public bool PasswordContainsUppercase { get; set; }

    /// <summary>
    /// 密码是否特殊符号
    /// </summary>
    public bool PasswordContainsNonAlphanumeric { get; set; }

    /// <summary>
    /// 密码的密码长度
    /// </summary>
    public int PasswordLength { get; set; }

    /// <summary>
    /// 是否需要重置密码
    /// </summary>
    public bool ResetPasswordFlag { get; set; }

    /// <summary>
    /// 隐藏敏感数据
    /// </summary>
    public bool HiddenSensitiveData { get; set; }
}