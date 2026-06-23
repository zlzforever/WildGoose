using Dapper;
using MySqlConnector;

namespace WildGoose.Application.Cache;

public static class MySqlUtil
{
    public static void CreateIfNotExists(string connectionString, string databaseName, string tableName)
    {
        var createTableFormat =
            // The index key prefix length limit is 767 bytes for InnoDB tables that use the REDUNDANT or COMPACT row format.
            // That is why we are using 'CHARACTER SET ascii COLLATE ascii_bin' column and index
            // https://dev.mysql.com/doc/refman/5.7/en/innodb-restrictions.html
            // - Add collation to the key column to make it case-sensitive
            "CREATE TABLE IF NOT EXISTS {0} (" +
            "`Id` varchar(449) CHARACTER SET ascii COLLATE ascii_bin NOT NULL," +
            "`AbsoluteExpiration` datetime(6) DEFAULT NULL," +
            "`ExpiresAtTime` datetime(6) NOT NULL," +
            "`SlidingExpirationInSeconds` bigint(20) DEFAULT NULL," +
            "`Value` longblob NOT NULL," +
            "PRIMARY KEY(`Id`)," +
            "KEY `Index_ExpiresAtTime` (`ExpiresAtTime`)" +
            ")";

        var builder = new MySqlConnectionStringBuilder(connectionString);
        databaseName = string.IsNullOrEmpty(databaseName) ? builder.Database : databaseName;
        var tableNameWithDatabase = string.Format("{0}{1}",
            string.IsNullOrEmpty(databaseName) ? "" : DelimitIdentifier(databaseName) + '.',
            DelimitIdentifier(tableName)
        );
        var createTable = string.Format(createTableFormat, tableNameWithDatabase);
        using var conn = new MySqlConnection(connectionString);
        conn.Execute(createTable);
    }

    public static string BuildInsertQuery(string connectionString, string databaseName, string tableName)
    {
        var builder = new MySqlConnectionStringBuilder(connectionString);
        databaseName = string.IsNullOrEmpty(databaseName) ? builder.Database : databaseName;
        var tableNameWithDatabase = string.Format("{0}{1}",
            string.IsNullOrEmpty(databaseName) ? "" : DelimitIdentifier(databaseName) + '.',
            DelimitIdentifier(tableName)
        );

        return $"""
                    INSERT IGNORE INTO {tableNameWithDatabase} (id, value, expiresattime)
                    VALUES (@Key, @Value, DATE_ADD(NOW(), INTERVAL @TtlSecond SECOND))
                """;
    }

    private static string DelimitIdentifier(string identifier)
    {
        return $"`{identifier}`";
    }

    private static string EscapeLiteral(string literal)
    {
        return literal.Replace("'", "''");
    }
}