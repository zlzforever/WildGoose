using System.ComponentModel.DataAnnotations;

namespace WildGoose.Application.User.Admin.V10.Command;

public class AddUserCommand
{
    public string[] Organizations { get; set; } = Array.Empty<string>();

    /// <summary>
    /// 电话
    /// </summary>
    [StringLength(13)]
    public string PhoneNumber { get; set; }

    /// <summary>
    /// 用户名
    /// </summary>
    [Required, StringLength(36), RegularExpression(NameLimiter.Pattern, ErrorMessage = NameLimiter.Message)]
    public string UserName { get; set; }

  
    /// <summary>
    /// 名
    /// </summary>
    [StringLength(256)]
    public string Name { get; set; }

    /// <summary>
    /// 密码
    /// </summary>
    [Required, StringLength(32)]
    public string Password { get; set; }

    /// <summary>
    /// 角色
    /// </summary>
    public List<string> Roles { get; set; } = new();
}