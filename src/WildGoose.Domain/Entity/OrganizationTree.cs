namespace WildGoose.Domain.Entity;

public class OrganizationTree
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string ParentId { get; set; }
    public string ParentName { get; set; }
    public string Path { get; set; }
    public string Branch { get; set; }
    public string Code { get; set; }
}