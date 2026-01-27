using System.ComponentModel.DataAnnotations;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace WildGoose.Application.User.Admin.V10.Command;

public record AddRoleCommand
{
    /// <summary>
    /// 角色标识
    /// </summary>

    [Required, StringLength(36)]
    public string RoleId { get; set; }

    /// <summary>
    /// 用户标识
    /// </summary>
    [Required, StringLength(36)]
    public string UserId { get; set; }
}