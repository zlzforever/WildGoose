using System.Text.RegularExpressions;

namespace WildGoose.Domain;

public static class Defaults
{
    public const string AdminRole = "admin";
    public const string OrganizationAdmin = "organization-admin";
    public const string UserAdmin = "user-admin";
    public static string AdminRoleId = "";
    public static string OrganizationAdminRoleId = "";
    public static string UserAdminRoleId = "";
    public static string OrganizationTableName = "";
    public static string OrganizationDetailTableName = "";
    public static string OrganizationUserTableName = "";
    public static string OrganizationAdministratorTableName = "";
    public static string OrganizationScopeTableName = "";
    public static readonly Regex AllowedUserNameRegex = new(@"^[a-zA-Z0-9\u4e00-\u9fa5]+$", RegexOptions.Compiled);
}