using System.ComponentModel.DataAnnotations;

namespace WildGoose.Application.Organization.Admin.V10.Command;

public class DeleteAdministratorCommand 
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
    public string UserId { get; set; }
}