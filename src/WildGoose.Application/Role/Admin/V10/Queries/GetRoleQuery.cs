using System.ComponentModel.DataAnnotations;

namespace WildGoose.Application.Role.Admin.V10.Queries;

public class GetRoleQuery
{
    /// <summary>
    /// 
    /// </summary>
    [Required, StringLength(36)]
    public string Id { get; set; }
}