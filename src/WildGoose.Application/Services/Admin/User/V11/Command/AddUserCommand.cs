using System.ComponentModel.DataAnnotations;
using WildGoose.Domain;

namespace WildGoose.Application.Services.Admin.User.V11.Command;

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
    [Required, StringLength(36), RegularExpression(Defaults.NameLimiter.Pattern, ErrorMessage = Defaults.NameLimiter.Message)]
    public string UserName { get; set; }

    /// <summary>
    /// 名
    /// </summary>
    [StringLength(256)]
    public string Name { get; set; }

    /// <summary>
    /// 密码: 允许不设密码，只使用短信登录
    /// </summary>
    [StringLength(32)]
    public string Password { get; set; }

    /// <summary>
    /// 用户扩展属性字典，key 为逻辑名称，从 UserPropertyMappings 配置中获取
    /// </summary>
    public Dictionary<string, string> ExtensionProperties { get; set; }
}