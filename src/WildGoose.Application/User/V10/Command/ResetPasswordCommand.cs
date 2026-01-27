using System.ComponentModel.DataAnnotations;

namespace WildGoose.Application.User.V10.Command;

public class ResetPasswordCommand
{
    [Required]
    [StringLength(40, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 6)]
    [DataType(DataType.Password)]
    [Display(Name = "新密码")]
    public string NewPassword { get; set; }

    [DataType(DataType.Password)]
    [Display(Name = "重复密码")]
    [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
    public string ConfirmPassword { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [Required, StringLength(40)]
    public string OriginalPassword { get; set; }
}