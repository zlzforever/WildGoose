using WildGoose.Domain.Entity;

namespace WildGoose.Application.User;

public static class Utils
{
    public static void SetPasswordInfo(UserExtension user, string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            user.PasswordLength = 0;
            user.PasswordContainsDigit = false;
            user.PasswordContainsLowercase = false;
            user.PasswordContainsUppercase = false;
            user.PasswordContainsNonAlphanumeric = false;
        }
        else
        {
            user.PasswordLength = password.Length;
            user.PasswordContainsDigit = password.Any(char.IsNumber);
            user.PasswordContainsLowercase = password.Any(char.IsLower);
            user.PasswordContainsUppercase = password.Any(char.IsUpper);
            user.PasswordContainsNonAlphanumeric = !password.All(char.IsLetterOrDigit);
        }
    }
}