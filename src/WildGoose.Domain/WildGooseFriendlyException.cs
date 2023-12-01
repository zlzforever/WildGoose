namespace WildGoose.Domain;

public class WildGooseFriendlyException : Exception
{
    public int Code { get; set; }

    public WildGooseFriendlyException(int code, string message, Exception innerException = null) : base(
        message,
        innerException)
    {
        Code = code;
    }
}