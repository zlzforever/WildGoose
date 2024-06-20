using System.ComponentModel.DataAnnotations;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace WildGoose.Application.Organization.V10.Queries;

public class GetSubListQuery
{
    /// <summary>
    /// 
    /// </summary>
    [StringLength(36)]
    public string ParentId { get; set; }

    /// <summary>
    /// 查询类型：all-所有，my-我的
    /// </summary>
    [StringLength(5)]
    public string Type { get; set; }
}