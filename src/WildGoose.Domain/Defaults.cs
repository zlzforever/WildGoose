using System.Text.RegularExpressions;

namespace WildGoose.Domain;

public static class Defaults
{
    public const string SuperOrUserAdminOrOrgAdminPolicy = "SUPER_OR_USER_ADMIN_OR_ORG_ADMIN";
    public const string UserAdminPolicy = "USER_ADMIN";
    public const string SuperPolicy = "SUPER";
    public const string AdminRole = "admin";
    public const string OrganizationAdmin = "organization-admin";

    /// <summary>
    /// 用户管理员
    /// </summary>
    public const string UserAdmin = "user-admin";

    /// <summary>
    /// 超级管理员 ID
    /// </summary>
    public static string AdminRoleId = "";

    /// <summary>
    /// 机构管理员 ID
    /// </summary>
    public static string OrganizationAdminRoleId = "";

    public static string UserAdminRoleId = "";
    public static string OrganizationTableName = "";
    public static string OrganizationDetailTableName = "";
    public static string OrganizationAdministratorTableName = "";
    public static string OrganizationScopeTableName = "";
    public static readonly Regex AllowedUserNameRegex = new(@"^[a-zA-Z0-9\u4e00-\u9fa5]+$", RegexOptions.Compiled);
    public static readonly string SecondTimeFormat = "yyyy-MM-dd HH:mm:ss";
}