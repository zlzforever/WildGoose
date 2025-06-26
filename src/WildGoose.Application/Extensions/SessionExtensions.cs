using WildGoose.Domain;

namespace WildGoose.Application.Extensions;

public static class SessionExtensions
{
    public static bool IsSupperAdmin(this ISession session)
    {
        return session.Roles.Contains(Defaults.AdminRole);
    }

    public static bool IsOrganizationAdmin(this ISession session)
    {
        return session.Roles.Contains(Defaults.OrganizationAdmin);
    }
}