using System.ComponentModel.DataAnnotations;
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace WildGoose.Application.Role.Admin.V10.Command;

public class UpdateRoleCommand
{
    /// <summary>
    /// 角色标识
    /// </summary>
    internal string Id { get; set; }

    /// <summary>
    /// 名称
    /// </summary>
    [Required, StringLength(255),
     RegularExpression(NameLimiter.Pattern, ErrorMessage = NameLimiter.Message)]
    public string Name { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [StringLength(512)]
    public string Description { get; set; }
}