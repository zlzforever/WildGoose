using System.ComponentModel.DataAnnotations;

namespace WildGoose.Application.Organization.Admin.V10.Queries;

public class GetDetailQuery
{
    /// <summary>
    /// 
    /// </summary>
    [Required, StringLength(36)]
    public string Id { get; set; }
}