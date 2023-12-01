namespace WildGoose.Application.User.Admin.V10.Queries;

public class GetUsersQuery
{
    public int Page { get; set; }
    public int Limit { get; set; }
    public string Q { get; set; }
    public string Status { get; set; }
    public string OrganizationId { get; set; }
}