using System.ComponentModel.DataAnnotations;

namespace WildGoose.Application.Role.Admin.V10.Queries;

public class GetRolesQuery
{
    /// <summary>
    /// 
    /// </summary>
    [StringLength(20)]
    public string Q { get; set; }

    public int Page { get; set; }
    public int Limit { get; set; }
}