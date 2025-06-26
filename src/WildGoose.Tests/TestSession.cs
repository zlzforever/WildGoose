using WildGoose.Domain;

namespace WildGoose.Tests;

public class TestSession : ISession
{
    public string TraceIdentifier { get; set; }
    public string UserId { get; set; }
    public string UserName { get; set; }
    public string UserDisplayName { get; set; }
    public string Email { get; set; }
    public string PhoneNumber { get; set; }
    public IReadOnlyCollection<string> Roles { get; set; }
    public IReadOnlyCollection<string> Subjects { get; set; }

    public void Load(ISession session)
    {
    }
}