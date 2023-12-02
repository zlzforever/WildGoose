namespace WildGoose.Application.User.Admin.V10.Dto;

public class UserDto
{
    public string Id { get; set; }
    public string UserName { get; set; }
    public string Name { get; set; }
    public string PhoneNumber { get; set; }
    public IEnumerable<string> Organizations { get; set; }
    public string CreationTime { get; set; }
    public bool Enabled { get; set; }

    public bool? IsAdministrator { get; set; }

    public IEnumerable<string> Roles { get; set; }
}