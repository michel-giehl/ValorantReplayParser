using CliReader.JsonExport;
using Replay.Tools.Logging;
using Microsoft.Extensions.Logging;
using Replay.Models.Errors;
using Serilog;

namespace CliReader;

internal sealed class CliApplication : IDisposable
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly Microsoft.Extensions.Logging.ILogger _logger;

    public CliApplication()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        _loggerFactory = LoggerFactory.Create(builder => builder
            .SetMinimumLevel(LogLevel.Information)
            .AddProvider(new SerilogLoggerProvider(Log.Logger)));
        _logger = _loggerFactory.CreateLogger("CliReader");
    }

    public int LogReplay(LogOptions options) => Execute(() =>
    {
        new ReplayLogRunner(_loggerFactory, _logger).Run(options);
        return 0;
    });

    public int ExportReplay(ExportOptions options) => Execute(() =>
    {
        _logger.LogInformation(
            "Exporting replay {ReplayPath} to {OutputDirectory}",
            options.ReplayPath,
            options.OutputDirectory);
        new ReplayExportRunner(_loggerFactory, new ReplayExportManifestWriter()).Run(options);
        _logger.LogInformation("Export complete.");
        return 0;
    });

    public void Dispose()
    {
        _loggerFactory.Dispose();
        Log.CloseAndFlush();
    }

    private int Execute(Func<int> action)
    {
        try
        {
            return action();
        }
        catch (ReplayParseException exception)
        {
            _logger.LogError(exception, "Failed to parse replay.");
            return 1;
        }
        catch (IOException exception)
        {
            _logger.LogError(exception, "Replay input or export output could not be accessed.");
            return 1;
        }
        catch (UnauthorizedAccessException exception)
        {
            _logger.LogError(exception, "Replay input or export output access was denied.");
            return 1;
        }
    }
}
