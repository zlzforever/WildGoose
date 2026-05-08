using System.ComponentModel.DataAnnotations;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace WildGoose.Application.Services.Admin.User.V10.Command;

public class EnableUserCommand
{
    /// <summary>
    /// 用户标识
    /// </summary>
    [Required, StringLength(36)]
    public string Id { get; set; }
}