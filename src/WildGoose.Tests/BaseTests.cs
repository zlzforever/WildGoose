using WildGoose.Domain;

namespace WildGoose.Tests;

public abstract class BaseTests
{
    // 预置中国大陆运营商有效手机号前缀（截至2026年主流号段）
    private static readonly List<string> PhonePrefixes = new List<string>
    {
        "130", "131", "132", "133", "134", "135", "136", "137", "138", "139",
        "145", "146", "147", "148", "149",
        "150", "151", "152", "153", "155", "156", "157", "158", "159",
        "162", "165", "166", "167",
        "170", "171", "172", "173", "175", "176", "177", "178",
        "180", "181", "182", "183", "184", "185", "186", "187", "188", "189",
        "191", "192", "193", "195", "196", "197", "198", "199"
    };

    /// <summary>
    /// 总公司
    /// </summary>
    protected const string RootOrg = "507f1f77bcf86cd799439030";

    /// <summary>
    /// 技术部
    /// </summary>
    protected const string TechOrg = "507f1f77bcf86cd799439031";

    /// <summary>
    /// 前端组
    /// </summary>
    protected const string FrontEndOrg = "507f1f77bcf86cd799439033";

    protected const string AdminUserId = "507f1f77bcf86cd799439020";
    protected const string TechAdminUserId = "507f1f77bcf86cd799439023";
    protected const string SaleAdminUserId = "507f1f77bcf86cd799439024";
    protected const string TechAndFrondEndUserId = "507f1f77bcf86cd799439023";
    protected const string BackendUserId = "507f1f77bcf86cd799439026";
    protected const string FrontendUserId = "507f1f77bcf86cd799439025";

    protected const string UserAdminUserId = "507f1f77bcf86cd799439021";

    // 角色ID（根据 testdata.sql）
    // protected const string ManagerRoleId = "507f1f77bcf86cd799439014";
    // protected const string EmployeeRoleId = "507f1f77bcf86cd799439015";
    // protected const string InternRoleId = "507f1f77bcf86cd799439016";
    protected const string ManagerRole = "manager";
    protected const string EmployeeRole = "employee";
    protected const string InternRole = "intern";

    protected static string CreateName()
    {
        var randomName = Guid.NewGuid().ToString("N").Substring(0, 6);
        var userName = $"Test{randomName}";
        return userName;
    }

    /// <summary>
    /// 生成符合规范的中国大陆11位手机号码
    /// </summary>
    /// <returns>随机手机号字符串</returns>
    protected static string GenerateChinesePhoneNumber()
    {
        // 创建随机数实例（使用时间戳种子保证随机性）
        Random random = new Random(Guid.NewGuid().GetHashCode());

        // 1. 随机选择一个有效前缀（3位）
        string prefix = PhonePrefixes[random.Next(PhonePrefixes.Count)];

        // 2. 生成后8位随机数字（0-9）
        string suffix = string.Empty;
        for (int i = 0; i < 8; i++)
        {
            suffix += random.Next(0, 10).ToString();
        }

        // 3. 拼接成完整11位手机号
        return prefix + suffix;
    }

    protected void LoadSuperAdmin(ISession session)
    {
        session.Load(new TestSession
        {
            UserId = "65965555b951f01bf13b1adc",
            UserDisplayName = "admin",
            UserName = "admin",
            Roles = [Defaults.AdminRole],
            Subjects = [Defaults.AdminRole, "65965555b951f01bf13b1adc"]
        });
    }

    protected void LoadOrganizationAdmin(ISession session)
    {
        session.Load(new TestSession
        {
            // 67e4aae1370fa2bc6de04fc3
            // 单元测试用户禁止删除 68540d88ed70c9c6b320673d
            UserId = "68540d88ed70c9c6b320673d",
            UserDisplayName = "单元测试用户禁止删除",
            UserName = "单元测试用户禁止删除",
            Roles = [Defaults.OrganizationAdmin],
            Subjects = [Defaults.OrganizationAdmin, "68540d88ed70c9c6b320673d"]
        });
    }

    protected void LoadDevelopAdmin(ISession session)
    {
        session.Load(new TestSession
        {
            UserId = "507f1f77bcf86cd799439023",
            UserDisplayName = "技术部经理",
            UserName = "技术部经理",
            Roles = [Defaults.OrganizationAdmin],
            Subjects = [Defaults.OrganizationAdmin, "507f1f77bcf86cd799439023"]
        });
    }

    protected void LoadUserAdmin(ISession session)
    {
        session.Load(new TestSession
        {
            UserId = "507f1f77bcf86cd799439021",
            UserDisplayName = "user_admin",
            UserName = "user_admin",
            Roles = [Defaults.UserAdmin],
            Subjects = [Defaults.UserAdmin, "507f1f77bcf86cd799439021"]
        });
    }

    protected void LoadNormalUser(ISession session)
    {
        session.Load(new TestSession
        {
            UserId = "68540d88ed70c9c6b3206731",
            UserDisplayName = "user1",
            UserName = "user1",
            Roles = [],
            Subjects = ["68540d88ed70c9c6b3206731"]
        });
    }

    protected void LoadInternalAddUserRole(ISession session)
    {
        session.Load(new TestSession
        {
            UserId = "68540d88ed70c9c6b3206731",
            UserDisplayName = "user1",
            UserName = "user1",
            Roles = ["内部添加用户"],
            Subjects = ["68540d88ed70c9c6b3206731", "内部添加用户"]
        });
    }
}