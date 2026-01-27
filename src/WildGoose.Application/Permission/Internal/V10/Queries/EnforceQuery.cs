using System.ComponentModel.DataAnnotations;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace WildGoose.Application.Permission.Internal.V10.Queries;

public class EnforceQuery
{
    /// <summary>
    /// 操作
    /// </summary>
    [StringLength(256)]
    public string Action { get; set; }

    /// <summary>
    /// 资源
    /// </summary>
    [StringLength(256)]
    public string Resource { get; set; }

    /// <summary>
    /// 策略生效范围
    /// </summary>
    [StringLength(256)]
    public string PolicyEffect { get; set; }
}