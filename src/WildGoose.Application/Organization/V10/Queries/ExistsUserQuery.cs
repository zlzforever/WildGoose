using System.ComponentModel.DataAnnotations;

namespace WildGoose.Application.Organization.V10.Queries;

public class ExistsUserQuery
{
    /// <summary>
    /// 
    /// </summary>
    [StringLength(36)]
    public string UserId { get; set; }

    /// <summary>
    /// 机构代码
    /// </summary>
    [StringLength(16)]
    public string Code { get; set; }
}