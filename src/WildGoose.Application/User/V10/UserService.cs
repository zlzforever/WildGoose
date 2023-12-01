using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WildGoose.Application.Extensions;
using WildGoose.Application.User.V10.Command;
using WildGoose.Domain;
using WildGoose.Domain.Entity;
using WildGoose.Infrastructure;

namespace WildGoose.Application.User.V10;

public class UserService : BaseService
{
    private readonly IPasswordValidator<WildGoose.Domain.Entity.User> _passwordValidator;
    private readonly UserManager<WildGoose.Domain.Entity.User> _userManager;

    public UserService(WildGooseDbContext dbContext, HttpSession session, IOptions<DbOptions> dbOptions,
        ILogger<UserService> logger, IPasswordValidator<WildGoose.Domain.Entity.User> passwordValidator,
        UserManager<WildGoose.Domain.Entity.User> userManager)
        : base(dbContext, session, dbOptions, logger)
    {
        _passwordValidator = passwordValidator;
        _userManager = userManager;
    }

    public async Task ResetPasswordByCaptchaAsync(ResetPasswordByCaptchaCommand command)
    {
        var password = command.NewPassword;
        var passwordValidatorResult =
            await _passwordValidator.ValidateAsync(_userManager, new WildGoose.Domain.Entity.User(), password);
        passwordValidatorResult.CheckErrors();

        var user = await _userManager.Users.FirstOrDefaultAsync(x =>
            x.PhoneNumber == command.PhoneNumber || x.UserName == command.PhoneNumber);
        if (user == null)
        {
            throw new WildGooseFriendlyException(1, "用户不存在");
        }

        var result =
            await _userManager.VerifyChangePhoneNumberTokenAsync(user, command.Captcha, command.PhoneNumber);
        if (!result)
        {
            // 验证失败的处理逻辑
            throw new WildGooseFriendlyException(1, "验证码不正确");
        }

        // 验证成功，可以允许用户更改密码
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        (await _userManager.ResetPasswordAsync(user, token, password)).CheckErrors();

        var extension = await DbContext.Set<UserExtension>()
            .FirstOrDefaultAsync(x => x.Id == user.Id);
        if (extension == null)
        {
            extension = new UserExtension { Id = user.Id };
            Utils.SetPasswordInfo(extension, password);
            await DbContext.AddAsync(extension);
        }
        else
        {
            Utils.SetPasswordInfo(extension, password);
        }

        await DbContext.SaveChangesAsync();
    }
}