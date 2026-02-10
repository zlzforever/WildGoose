using System.Text.Json.Serialization;
using WildGoose.Domain.Entity;

namespace WildGoose.Application;

/// <summary>
/// 组织数据传输对象
/// 封装组织的基础层级信息
/// </summary>
public class OrganizationEntity : IPath
{
    /// <summary>
    /// 组织唯一标识
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// 组织名称
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 上级组织唯一标识
    /// 顶级组织可为空或指定默认值
    /// </summary>
    public string ParentId { get; set; }

    /// <summary>
    /// 是否包含子组织
    /// 用于组织树的层级展示控制
    /// </summary>
    public bool HasChild { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [JsonIgnore]
    public string Path { get; set; }

    public string Code { get; set; }

    public int Level { get; set; }

    public static OrganizationEntity Build(OrganizationDetail x)
    {
        return new OrganizationEntity
        {
            Id = x.Id,
            Name = x.Name,
            HasChild = x.HasChild,
            ParentId = x.ParentId,
            Path = x.Path,
            Code = x.Code,
            Level = x.Level
        };
    }
}