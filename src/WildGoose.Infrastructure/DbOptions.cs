namespace WildGoose.Infrastructure;

public class DbOptions
{
    public required string TablePrefix { get; set; }
    public bool UseUnderScoreCase { get; set; }
    public required string ConnectionString { get; set; }
    public required string DatabaseType { get; set; }
    public bool EnableSensitiveDataLogging { get; set; }
}