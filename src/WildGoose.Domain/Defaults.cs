using System.Text.RegularExpressions;

namespace WildGoose.Domain;

public static class Defaults
{
    public const string SuperOrUserAdminOrOrgAdminPolicy = "SUPER_OR_USER_ADMIN_OR_ORG_ADMIN";
    public const string SuperPolicy = "SUPER";
    public const string Admin = "admin";
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
    public static bool DisablePasswordLogin = false;

    public static string ApiName;

    public static class NameLimiter
    {
        public const string Pattern = "[0-9-_a-zA-Z\\u4e00-\\u9fa5]+";
        public const string Message = "只能使用汉字、英文、中划线、下划线，不能有空格、@、￥等特殊字符";
    }
}