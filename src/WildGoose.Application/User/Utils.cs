using WildGoose.Domain.Entity;

namespace WildGoose.Application.User;

public static class Utils
{
    public static void SetPasswordInfo(UserExtension user, string password)
    {
        user.PasswordLength = password.Length;
        user.PasswordContainsDigit = password.Any(char.IsNumber);
        user.PasswordContainsLowercase = password.Any(char.IsLower);
        user.PasswordContainsUppercase = password.Any(char.IsUpper);
        user.PasswordContainsNonAlphanumeric = !password.All(char.IsLetterOrDigit);
    }
}