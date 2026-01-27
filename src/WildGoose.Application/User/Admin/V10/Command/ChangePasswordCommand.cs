using System.ComponentModel.DataAnnotations;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace WildGoose.Application.User.Admin.V10.Command;

public record ChangePasswordCommand
{
    internal string Id { get; set; }

    [Required]
    [StringLength(24, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 6)]
    [DataType(DataType.Password)]
    [Display(Name = "新密码")]
    public string NewPassword { get; set; }

    [DataType(DataType.Password)]
    [Display(Name = "重复密码")]
    [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
    public string ConfirmPassword { get; set; }
}