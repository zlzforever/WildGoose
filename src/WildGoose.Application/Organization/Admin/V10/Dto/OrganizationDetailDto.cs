using WildGoose.Application.Dto;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace WildGoose.Application.Organization.Admin.V10.Dto;

public class OrganizationDetailDto
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Code { get; set; }

    /// <summary>
    /// 父节点标识
    /// </summary>
    public OrganizationSimpleDto Parent { get; set; }

    public string Address { get; set; }

    public string Description { get; set; }

    public string Metadata { get; set; }

    // ReSharper disable once CollectionNeverQueried.Global
    public List<string> Scope { get; set; }

    // ReSharper disable once CollectionNeverQueried.Global
    public List<UserDto> Administrators { get; set; }
}