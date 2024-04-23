using System.ComponentModel.DataAnnotations;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace WildGoose.Application.User.Admin.V10.Command;

public class UpdateUserCommand
{
    public string[] Organizations { get; set; } = Array.Empty<string>();

    internal string Id { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [StringLength(36)]
    public string Code { get; set; }

    /// <summary>
    /// 电话
    /// </summary>
    [StringLength(13)]
    public string PhoneNumber { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [StringLength(256)]
    public string Name { get; set; }

    /// <summary>
    /// 职位
    /// </summary>
    [StringLength(256)]
    public string Title { get; set; }

    /// <summary>
    /// 邮箱
    /// </summary>
    [StringLength(256)]
    public string Email { get; set; }

    /// <summary>
    /// 用户名
    /// </summary>
    [Required, StringLength(36), RegularExpression(NameLimiter.Pattern, ErrorMessage = NameLimiter.Message)]
    public string UserName { get; set; }

    /// <summary>
    /// 角色
    /// </summary>
    public string[] Roles { get; set; } = Array.Empty<string>();

    /// <summary>
    ///  
    /// </summary>
    public bool HiddenSensitiveData { get; set; }

    /// <summary>
    /// 离职时间
    /// </summary>
    public DateTimeOffset? DepartureTime { get; set; }
}