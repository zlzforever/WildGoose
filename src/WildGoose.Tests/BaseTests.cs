using WildGoose.Domain;

namespace WildGoose.Tests;

public class BaseTests
{
    protected void LoadAdmin(ISession session)
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
            UserId = "68540d88ed70c9c6b320673d",
            UserDisplayName = "单元测试用户禁止删除",
            UserName = "单元测试用户禁止删除",
            Roles = [Defaults.OrganizationAdmin],
            Subjects = [Defaults.OrganizationAdmin, "67e4aae1370fa2bc6de04fc3"]
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
}