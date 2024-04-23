using System.ComponentModel.DataAnnotations;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace WildGoose.Application.User.V10.Command;

public class ResetPasswordByCaptchaCommand
{
    [Required]
    [StringLength(24, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 6)]
    [DataType(DataType.Password)]
    [Display(Name = "新密码")]
    public string NewPassword { get; set; }

    [DataType(DataType.Password)]
    [Display(Name = "重复密码")]
    [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
    public string ConfirmPassword { get; set; }

    /// <summary>
    /// 手机号
    /// </summary>
    [Required, StringLength(13)]
    public string PhoneNumber { get; set; }

    /// <summary>
    /// 验证码
    /// </summary>
    [Required]
    [StringLength(6)]
    public string Captcha { get; set; }
}