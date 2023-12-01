using System.ComponentModel.DataAnnotations;

namespace WildGoose.Application.Organization.Admin.V10.Command;

public class DeleteOrganizationCommand
{
    /// <summary>
    /// 
    /// </summary>
    [Required, StringLength(36)]
    public string Id { get; set; }
}