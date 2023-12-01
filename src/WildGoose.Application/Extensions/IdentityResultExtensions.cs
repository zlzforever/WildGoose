using Microsoft.AspNetCore.Identity;
using WildGoose.Domain;

namespace WildGoose.Application.Extensions;

public static class IdentityResultExtensions
{
    public static void CheckErrors(this IdentityResult identityResult, string defaultMessage = "未知错误")
    {
        if (identityResult.Succeeded)
        {
            return;
        }

        if (identityResult.Errors == null)
        {
            throw new WildGooseFriendlyException(1, defaultMessage);
        }

        throw new WildGooseFriendlyException(1,
            string.Join(Environment.NewLine, identityResult.Errors.Select(x => x.Description)));
    }
}