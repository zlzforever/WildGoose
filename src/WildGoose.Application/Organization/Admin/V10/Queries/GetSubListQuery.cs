using System.ComponentModel.DataAnnotations;

namespace WildGoose.Application.Organization.Admin.V10.Queries;

public class GetSubListQuery
{
    /// <summary>
    /// 
    /// </summary>
    [StringLength(36)]
    public string ParentId { get; set; }
}