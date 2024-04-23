using System.ComponentModel.DataAnnotations;
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace WildGoose.Application.Role.Admin.V10.Command;

public class DeleteRoleCommand
{
    /// <summary>
    /// 角色标识
    /// </summary>
    [Required, StringLength(36)]
    public string Id { get; set; }
}