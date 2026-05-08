using System.ComponentModel.DataAnnotations;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace WildGoose.Application.Services.Admin.Organization.V10.Command;

public record AddAdministratorCommand
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