using System.ComponentModel.DataAnnotations;

namespace WildGoose.Application.User.Admin.V10.Command;

public class DeleteUserCommand
{
    /// <summary>
    /// 
    /// </summary>
    [Required, StringLength(36)]
    public string Id { get; set; }
}