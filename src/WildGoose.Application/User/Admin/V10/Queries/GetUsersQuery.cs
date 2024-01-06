using System.ComponentModel.DataAnnotations;

namespace WildGoose.Application.User.Admin.V10.Queries;

public class GetUsersQuery
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
}