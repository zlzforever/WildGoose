namespace WildGoose;

public class DbContextConfig
{
    public required string TablePrefix { get; set; }
    public bool UseUnderScoreCase { get; set; }
    public required string ConnectionString { get; set; }
    public required string DatabaseType { get; set; }
}