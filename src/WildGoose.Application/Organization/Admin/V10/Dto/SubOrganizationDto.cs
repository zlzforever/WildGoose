namespace WildGoose.Application.Organization.Admin.V10.Dto;

public class SubOrganizationDto
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string ParentId { get; set; }
    public string ParentName { get; set; }
    public bool HasChild { get; set; }
}