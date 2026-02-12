namespace ReceptyOks.Configuration;

using ReceptyOks.Data;
using Serilog;

/// <summary>
/// Extension methods for configuring logging services.
/// </summary>
internal static class LoggingServiceExtensions
{
    /// <summary>
    /// Configures Serilog logging with SQLite sink and debug output.
    /// </summary>
    /// <param name="builder">The MAUI app builder.</param>
    /// <param name="appSettings">Application settings containing database configuration.</param>
    /// <returns>The builder for chaining.</returns>
    public static MauiAppBuilder ConfigureSerilog(this MauiAppBuilder builder, AppSettings appSettings)
    {
        ArgumentNullException.ThrowIfNull(appSettings);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .WriteTo.Sink(new SQLiteSink(appSettings.Database.LocalDatabasePath))
#if DEBUG
            .WriteTo.Debug()
#endif
      .CreateLogger();

        builder.Logging.AddSerilog(dispose: true);

        // Register Serilog logger instance for DI consumers that request Serilog.ILogger
        builder.Services.AddSingleton<Serilog.ILogger>(Log.Logger);
        return builder;
    }
}
