using System.ComponentModel.DataAnnotations;
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace WildGoose.Application.Role.Admin.V10.Command;

public class AddAssignableRoleCommand
{
    /// <summary>
    /// 
    /// </summary>
    internal string Id { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public List<string> AssignableRoleIds { get; set; }
}