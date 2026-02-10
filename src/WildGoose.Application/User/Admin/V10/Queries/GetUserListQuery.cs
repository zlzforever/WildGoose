using System.ComponentModel.DataAnnotations;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace WildGoose.Application.User.Admin.V10.Queries;

public class GetUserListQuery
{
    public int Page { get; set; }
    public int Limit { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [StringLength(50)]
    public string Q { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [StringLength(50)]
    public string Status { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [StringLength(36)]
    public string OrganizationId { get; set; }

    /// <summary>
    /// 是否递归查询
    /// </summary>
    public bool IsRecursive { get; set; }
}