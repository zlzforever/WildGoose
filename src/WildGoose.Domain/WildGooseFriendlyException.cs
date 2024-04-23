namespace WildGoose.Domain;

public class WildGooseFriendlyException(int code, string message, Exception innerException = null) : Exception(message,
    innerException)
{
    public int Code { get; set; } = code;

    public string LogInfo { get; set; }
}