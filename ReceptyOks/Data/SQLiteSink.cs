using Serilog.Core;
using Serilog.Events;
using SQLite;
using System.Text.Json;

namespace ReceptyOks.Data;

public class SQLiteSink : ILogEventSink
{
    private readonly string _databasePath;
    private readonly object _syncRoot = new();

    public SQLiteSink(string databasePath)
    {
        _databasePath = databasePath;
    }

    public void Emit(LogEvent logEvent)
    {
        lock (_syncRoot)
        {
            try
            {
                using var db = new SQLiteConnection(_databasePath);

                var logEntry = new LogEntry
                {
                    Timestamp = logEvent.Timestamp.UtcDateTime,
                    Level = logEvent.Level.ToString(),
                    Message = logEvent.RenderMessage(),
                    Exception = logEvent.Exception?.ToString(),
                    Properties = logEvent.Properties.Count > 0
                        ? JsonSerializer.Serialize(logEvent.Properties.ToDictionary(
                            p => p.Key,
                            p => p.Value.ToString()))
                        : null
                };

                db.Insert(logEntry);
            }
            catch
            {
                // Prevent logging errors from crashing the app
            }
        }
    }
}
