using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace WildGoose.Application;

public class NewUserValidator<TUser> : UserValidator<TUser>
    where TUser : IdentityUser
{
    public override async Task<IdentityResult> ValidateAsync(UserManager<TUser> manager, TUser user)
    {
        var errors = await ValidateUserName(manager, user);
        if (manager.Options.User.RequireUniqueEmail)
        {
            errors = await ValidateEmail(manager, user, errors);
        }

        return errors?.Count > 0 ? IdentityResult.Failed(errors.ToArray()) : IdentityResult.Success;
    }

    private async Task<List<IdentityError>> ValidateUserName(UserManager<TUser> manager, TUser user)
    {
        var errors = new List<IdentityError>();
        var userName = await manager.GetUserNameAsync(user);
        if (string.IsNullOrWhiteSpace(userName))
        {
            errors.Add(Describer.InvalidUserName(userName));
        }
        else if (!string.IsNullOrEmpty(manager.Options.User.AllowedUserNameCharacters) &&
                 !Regex.IsMatch(userName, manager.Options.User.AllowedUserNameCharacters))
        {
            errors.Add(Describer.InvalidUserName(userName));
        }
        else
        {
            var userId = await manager.GetUserIdAsync(user);
            var normalizeName = manager.NormalizeName(userName);
            if (await manager.Users.AnyAsync(x => x.Id != userId && x.NormalizedUserName == normalizeName))
            {
                errors.Add(Describer.DuplicateUserName(userName));
            }

            if (string.IsNullOrWhiteSpace(user.PhoneNumber))
            {
                user.PhoneNumber = null;
            }
            else
            {
                if (await manager.Users.AnyAsync(x => x.Id != userId && x.PhoneNumber == user.PhoneNumber))
                {
                    errors.Add(new IdentityError
                    {
                        Code = "DuplicatePhoneNumber",
                        Description = "手机号已经存在"
                    });
                }
            }
        }

        return errors;
    }

    // make sure email is not empty, valid, and unique
    private async Task<List<IdentityError>> ValidateEmail(UserManager<TUser> manager, TUser user,
        List<IdentityError> errors)
    {
        var email = await manager.GetEmailAsync(user);
        if (string.IsNullOrWhiteSpace(email))
        {
            user.Email = null;
            errors ??= new List<IdentityError>();
            errors.Add(Describer.InvalidEmail(email));
            return errors;
        }

        if (!new EmailAddressAttribute().IsValid(email))
        {
            errors ??= new List<IdentityError>();
            errors.Add(Describer.InvalidEmail(email));
            return errors;
        }

        var userId = await manager.GetUserIdAsync(user);
        var normalizeEmail = manager.NormalizeEmail(email);
        if (await manager.Users.AnyAsync(x => x.Id != userId && x.NormalizedEmail == normalizeEmail))
        {
            errors ??= new List<IdentityError>();
            errors.Add(Describer.DuplicateEmail(email));
        }

        return errors;
    }
}