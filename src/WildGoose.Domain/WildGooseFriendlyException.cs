namespace WildGoose.Domain;

public class WildGooseFriendlyException(int code, string message, Exception innerException = null) : Exception(message,
    innerException)
{
    public static WildGooseFriendlyException From(int code) =>
        new(code, ErrorCodes.GetMessage(code));

    public static WildGooseFriendlyException From(int code, string customMessage) =>
        new(code, customMessage);

    public WildGooseFriendlyException(int code) : this(code, ErrorCodes.GetMessage(code))
    {
    }

    public int Code { get; set; } = code;

    public string LogInfo { get; set; }
}