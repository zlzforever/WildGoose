using WildGoose.Domain;

namespace WildGoose.Application.Extensions;

public static class SessionExtensions
{
    extension(ISession session)
    {
        private bool IsSupperAdmin()
        {
            return session.Roles.Contains(Defaults.AdminRole);
        }

        public bool IsSupperAdminOrUserAdmin()
        {
            return session.IsSupperAdmin() || session.IsUserAdmin();
        }

        private bool IsUserAdmin()
        {
            return session.Roles.Contains(Defaults.UserAdmin);
        }

        public bool IsOrganizationAdmin()
        {
            return session.Roles.Contains(Defaults.OrganizationAdmin);
        }
    }
}