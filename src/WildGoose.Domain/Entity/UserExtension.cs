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

    /// <summary>
    /// 扩展字段01
    /// </summary>
    public string Property01 { get; set; }

    /// <summary>
    /// 扩展字段02
    /// </summary>
    public string Property02 { get; set; }

    /// <summary>
    /// 扩展字段03
    /// </summary>
    public string Property03 { get; set; }

    /// <summary>
    /// 扩展字段04
    /// </summary>
    public string Property04 { get; set; }

    /// <summary>
    /// 扩展字段05
    /// </summary>
    public string Property05 { get; set; }

    /// <summary>
    /// 扩展字段06
    /// </summary>
    public string Property06 { get; set; }

    /// <summary>
    /// 扩展字段07
    /// </summary>
    public string Property07 { get; set; }

    /// <summary>
    /// 扩展字段08
    /// </summary>
    public string Property08 { get; set; }

    /// <summary>
    /// 扩展字段09
    /// </summary>
    public string Property09 { get; set; }

    public void SetPasswordInfo(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            PasswordLength = 0;
            PasswordContainsDigit = false;
            PasswordContainsLowercase = false;
            PasswordContainsUppercase = false;
            PasswordContainsNonAlphanumeric = false;
        }
        else
        {
            PasswordLength = password.Length;
            PasswordContainsDigit = password.Any(char.IsNumber);
            PasswordContainsLowercase = password.Any(char.IsLower);
            PasswordContainsUppercase = password.Any(char.IsUpper);
            PasswordContainsNonAlphanumeric = !password.All(char.IsLetterOrDigit);
        }
    }
}