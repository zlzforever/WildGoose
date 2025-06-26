namespace WildGoose.Domain;

public interface ISession
{
    public string TraceIdentifier { get; }

    public string UserId { get; }

    public string UserName { get; }

    public string UserDisplayName { get; }

    public string Email { get; }

    public string PhoneNumber { get; }

    public IReadOnlyCollection<string> Roles { get; }

    public IReadOnlyCollection<string> Subjects { get; }
    
    void Load(ISession session);
}