using System.ComponentModel.DataAnnotations;
using WildGoose.Domain;

// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace WildGoose.Application.Services.Admin.Role.V10.Command;

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
     RegularExpression(Defaults.NameLimiter.Pattern, ErrorMessage = Defaults.NameLimiter.Message)]
    public string Name { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [StringLength(512)]
    public string Description { get; set; }
}