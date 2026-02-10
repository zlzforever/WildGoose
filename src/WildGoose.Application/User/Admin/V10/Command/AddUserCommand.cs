using System.ComponentModel.DataAnnotations;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace WildGoose.Application.User.Admin.V10.Command;

public class AddUserCommand
{
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
    public List<string> Roles { get; set; }

    /// <summary>
    /// 所属机构
    /// </summary>
    public List<string> Organizations { get; set; }
}