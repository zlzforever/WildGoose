using System.ComponentModel.DataAnnotations;

namespace WildGoose.Application.Organization.V10.Queries;

public class IsUserInOrganizationWithInheritanceQuery
{
    /// <summary>
    /// 
    /// </summary>
    [StringLength(36)]
    public string UserId { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [StringLength(10)]
    public string Code { get; set; }
}