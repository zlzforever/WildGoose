using System.Text.Json;

namespace WildGoose.Application.User.V10.Dto;

public class OrganizationDto
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Code { get; set; }
    public string ParentId { get; set; }
    public string ParentName { get; set; }
    public bool HasChild { get; set; }
    public JsonElement Metadata { get; set; }
    public List<string> Scope { get; set; }
}