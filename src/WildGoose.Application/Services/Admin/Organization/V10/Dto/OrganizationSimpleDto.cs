namespace WildGoose.Application.Services.Admin.Organization.V10.Dto;

public class OrganizationSimpleDto
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string ParentId { get; set; }
    public bool HasChild { get; set; }
}