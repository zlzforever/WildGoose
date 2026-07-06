using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace WildGoose.Domain;

public static class Defaults
{
    public static ILogger Logger = NullLogger.Instance;
    public const string SuperOrUserAdminOrOrgAdminPolicy = "SUPER_OR_USER_ADMIN_OR_ORG_ADMIN";
    public const string SuperPolicy = "SUPER";
    public static readonly string AdminRole;
    public static readonly string OrganizationAdminRole;

    /// <summary>
    /// 用户管理员
    /// </summary>
    public static readonly string UserAdminRole;

    static Defaults()
    {
        AdminRole = Environment.GetEnvironmentVariable("SUPER_ADMIN_ROLE");
        AdminRole = string.IsNullOrWhiteSpace(AdminRole)
            ? "admin"
            : AdminRole;
        OrganizationAdminRole = Environment.GetEnvironmentVariable("ORGANIZATION_ADMIN_ROLE");
        OrganizationAdminRole = string.IsNullOrWhiteSpace(OrganizationAdminRole)
            ? "organization-admin"
            : OrganizationAdminRole;
        UserAdminRole = Environment.GetEnvironmentVariable("USER_ADMIN_ROLE");
        UserAdminRole = string.IsNullOrWhiteSpace(UserAdminRole) ? "user-admin" : UserAdminRole;
    }

    /// <summary>
    /// 超级管理员 ID
    /// </summary>
    public static string AdminRoleId = "";

    /// <summary>
    /// 机构管理员 ID
    /// </summary>
    public static string OrganizationAdminRoleId = "";

    /// <summary>
    /// 用户管理员 ID
    /// </summary>
    public static string UserAdminRoleId = "";


    public static string OrganizationTableName = "";
    public static string OrganizationDetailTableName = "";
    public static string OrganizationAdministratorTableName = "";
    public static string OrganizationScopeTableName = "";

    public static readonly Regex AllowedUserNameRegex = new(@"^[a-zA-Z0-9\u4e00-\u9fa5]+$", RegexOptions.Compiled);
    public static readonly string SecondTimeFormat = "yyyy-MM-dd HH:mm:ss";
    public static bool DisablePasswordLogin = false;

    public static string ApiName;

    public static class NameLimiter
    {
        public const string Pattern = "[0-9-_a-zA-Z\\u4e00-\\u9fa5]+";
        public const string Message = "只能使用汉字、英文、中划线、下划线，不能有空格、@、￥等特殊字符";
    }
}