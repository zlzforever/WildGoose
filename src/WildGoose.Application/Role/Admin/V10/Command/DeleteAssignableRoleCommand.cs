using System.ComponentModel.DataAnnotations;

namespace WildGoose.Application.Role.Admin.V10.Command;

public class DeleteAssignableRoleCommand
{
    /// <summary>
    /// 
    /// </summary>
    [Required, StringLength(36)]
    public string Id { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [Required, StringLength(36)]
    public string AssignableRoleId { get; set; }
}