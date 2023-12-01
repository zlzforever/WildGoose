using System.ComponentModel.DataAnnotations;

namespace WildGoose.Application.Role.Admin.V10.Command;

public class UpdateStatementCommand
{
    /// <summary>
    /// 角色标识
    /// </summary>
    internal string Id { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [StringLength(6000)]
    public string Statement { get; set; }
}