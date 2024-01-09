using System.Text.Json;

namespace WildGoose.Application.Organization.V10.Dto;

public class SubOrganizationDto
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string ParentId { get; set; }
    public string ParentName { get; set; }
    public bool HasChild { get; set; }
    public JsonDocument Metadata { get; set; }
    public List<string> Scope { get; set; }
}