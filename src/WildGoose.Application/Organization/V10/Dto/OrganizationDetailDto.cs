namespace WildGoose.Application.Organization.V10.Dto;

// ReSharper disable UnusedAutoPropertyAccessor.Global
public class OrganizationDetailDto
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Code { get; set; }
    public string ParentId { get; set; }
    public string ParentName { get; set; }
    public string Path { get; set; }
    public string Branch { get; set; }
}