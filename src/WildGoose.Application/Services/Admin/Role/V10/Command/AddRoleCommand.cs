using System.ComponentModel.DataAnnotations;

// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace WildGoose.Application.Services.Admin.Role.V10.Command;

public class AddRoleCommand
{
    //public string DomainId { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [Required, StringLength(100)]
    public string Name { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [StringLength(512)]
    public string Description { get; set; }
}