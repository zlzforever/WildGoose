namespace WildGoose.Domain.Entity;

public class OrganizationDetail
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string ParentId { get; set; }
    public string ParentName { get; set; }
    public string Path { get; set; }
    public string Branch { get; set; }
    public string Code { get; set; }
    public string Metadata { get; set; }
    public long NId { get; set; }
    public int Level { get; set; }
    public bool HasChild { get; set; }
}