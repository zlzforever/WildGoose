using System.Text.Json;

namespace WildGoose.Domain.Entity;

public class Organization : IDeletion
{
    public string Id { get; set; }

    /// <summary>
    /// 名称
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 编号，在地信行业一般使用行政区代码
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// 所在地址
    /// </summary>
    public string Address { get; set; }

    /// <summary>
    /// 描述
    /// </summary>
    public string Description { get; set; }

    public Organization Parent { get; set; }

    public string Metadata { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTimeOffset? CreationTime { get; set; }

    /// <summary>
    /// 创建人标识
    /// </summary>
    public string CreatorId { get; set; }

    /// <summary>
    /// 创建人名称
    /// </summary>
    public string CreatorName { get; set; }

    /// <summary>
    /// 最后修改人标识
    /// </summary>
    public string LastModifierId { get; set; }

    /// <summary>
    /// 最后修改人名称
    /// </summary>
    public string LastModifierName { get; set; }

    /// <summary>
    /// 最后修改时间
    /// </summary>
    public DateTimeOffset? LastModificationTime { get; set; }

    /// <summary>
    /// 是否已经删除
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// 删除人标识
    /// </summary>
    public string DeleterId { get; set; }

    /// <summary>
    /// 删除人
    /// </summary>
    public string DeleterName { get; set; }

    /// <summary>
    /// 删除时间
    /// </summary>
    public DateTimeOffset? DeletionTime { get; set; }

    /// <summary>
    /// 自增 ID
    /// </summary>
    public long? NId { get; set; }

    // /// <summary>
    // /// 排序字段，越小越前
    // /// </summary>
    // public int Order { get; set; }

    public void SetMetadata(string metadata)
    {
        var value = string.IsNullOrEmpty(metadata)
            ? string.Empty
            : JsonSerializer.Serialize(JsonSerializer.Deserialize<JsonDocument>(metadata),
                new JsonSerializerOptions
                {
                    WriteIndented = false
                });
        Metadata = value;
    }
}