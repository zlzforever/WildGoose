using Microsoft.IdentityModel.Abstractions;

namespace WildGoose;

public class SerilogIdentityLogger(ILogger logger) : IIdentityLogger
{
    public bool IsEnabled(EventLogLevel eventLogLevel)
    {
        return logger.IsEnabled(GetLogLevel(eventLogLevel));
    }

    public void Log(LogEntry entry)
    {
        logger.LogInformation(entry.Message);
    }

    private LogLevel GetLogLevel(EventLogLevel eventLogLevel)
    {
        if (eventLogLevel == EventLogLevel.Informational)
        {
            return LogLevel.Information;
        }

        if (eventLogLevel == EventLogLevel.Warning)
        {
            return LogLevel.Warning;
        }

        if (eventLogLevel == EventLogLevel.Critical)
        {
            return LogLevel.Critical;
        }

        if (eventLogLevel == EventLogLevel.Verbose)
        {
            return LogLevel.Trace;
        }

        if (eventLogLevel == EventLogLevel.LogAlways)
        {
            return LogLevel.Information;
        }

        if (eventLogLevel == EventLogLevel.Error)
        {
            return LogLevel.Error;
        }

        return LogLevel.Information;
    }
}