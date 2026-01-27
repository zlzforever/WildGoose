using System.ComponentModel.DataAnnotations;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace WildGoose.Application.User.Admin.V10.Command;

public class DisableUserCommand
{
    /// <summary>
    /// 用户标识
    /// </summary>
    [Required, StringLength(36)]
    public string Id { get; set; }
}