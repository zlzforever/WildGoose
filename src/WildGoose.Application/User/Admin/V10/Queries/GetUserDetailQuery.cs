using System.ComponentModel.DataAnnotations;

namespace WildGoose.Application.User.Admin.V10.Queries;

public class GetUserDetailQuery
{
    /// <summary>
    /// 
    /// </summary>
    [Required, StringLength(36)]
    public string Id { get; set; }
}