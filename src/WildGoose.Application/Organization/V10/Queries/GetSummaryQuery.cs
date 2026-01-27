using System.ComponentModel.DataAnnotations;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace WildGoose.Application.Organization.V10.Queries;

public class GetSummaryQuery
{
    /// <summary>
    /// 
    /// </summary>
    [Required, StringLength(36)]
    public string Id { get; set; }
}