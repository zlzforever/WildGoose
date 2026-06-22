using System.ComponentModel.DataAnnotations;
using WildGoose.Domain;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace WildGoose.Application.Services.Admin.User.V10.Command;

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
    [Required, StringLength(36), RegularExpression(Defaults.NameLimiter.Pattern, ErrorMessage =
         Defaults.NameLimiter.Message)]
    public string UserName { get; set; }

    /// <summary>
    /// 名
    /// </summary>
    [StringLength(256)]
    public string Name { get; set; }

    /// <summary>
    /// 密码: 有不设密码的场景，只使用短信等登录
    /// </summary>
    [StringLength(32)]
    public string Password { get; set; }

    /// <summary>
    /// 角色
    /// </summary>
    public List<string> Roles { get; set; }

    /// <summary>
    /// 所属机构
    /// </summary>
    public List<string> Organizations { get; set; }

    /// <summary>
    /// 用户扩展属性字典，key 为逻辑名称，从 UserPropertyMappings 配置中获取
    /// </summary>
    public Dictionary<string, string> Properties { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [Required, StringLength(40)]
    public string Nonce { get; set; }
}