using System.ComponentModel.DataAnnotations;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace WildGoose.Application.Organization.Admin.V10.Command;

public record AddOrganizationCommand
{
    /// <summary>
    /// 
    /// </summary>
    [StringLength(36)]
    public string ParentId { get; set; }

    /// <summary>
    /// 名称
    /// </summary>
    [Required, StringLength(50),
     RegularExpression(NameLimiter.Pattern, ErrorMessage = NameLimiter.Message)]
    public string Name { get; set; }

    /// <summary>
    /// 编号
    /// </summary>
    [StringLength(64)]
    public string Code { get; set; }

    /// <summary>
    /// 描述
    /// </summary>
    [StringLength(256)]
    public string Address { get; set; }

    /// <summary>
    /// 描述
    /// </summary>
    [StringLength(256)]
    public string Description { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [StringLength(2000)]
    public string Metadata { get; set; }
    
    // public int Order { get; set; }

    public string[] Scope { get; set; } = Array.Empty<string>();
}