using WildGoose.Domain;

namespace WildGoose.Application.Extensions;

public static class SessionExtensions
{
    extension(ISession session)
    {
        private bool IsSuperAdmin()
        {
            return session.Roles.Contains(Defaults.AdminRole);
        }

        public bool IsSuperAdminOrUserAdmin()
        {
            return session.IsSuperAdmin() || session.IsUserAdmin();
        }

        private bool IsUserAdmin()
        {
            return session.Roles.Contains(Defaults.UserAdminRole);
        }

        public bool IsOrganizationAdmin()
        {
            return session.Roles.Contains(Defaults.OrganizationAdminRole);
        }
    }
}