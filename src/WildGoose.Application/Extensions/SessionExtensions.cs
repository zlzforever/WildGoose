using WildGoose.Domain;

namespace WildGoose.Application.Extensions;

public static class SessionExtensions
{
    extension(ISession session)
    {
        private bool IsSuperAdmin()
        {
            return session.Roles.Contains(Defaults.Admin);
        }

        public bool IsSuperAdminOrUserAdmin()
        {
            return session.IsSuperAdmin() || session.IsUserAdmin();
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