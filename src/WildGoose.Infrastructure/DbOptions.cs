namespace WildGoose.Infrastructure;

public class DbOptions
{
    public string TablePrefix { get; set; }
    public string DatabaseType { get; set; }
    public bool UseUnderScoreCase { get; set; }
    public bool EnableSensitiveDataLogging { get; set; }
    public string ConnectionString { get; set; }
}