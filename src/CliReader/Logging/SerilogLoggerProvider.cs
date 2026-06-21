using Microsoft.Extensions.Logging;
using Serilog.Events;

namespace CliReader;

internal sealed class SerilogLoggerProvider : ILoggerProvider
{
    private readonly Serilog.ILogger _logger;

    public SerilogLoggerProvider(Serilog.ILogger logger)
    {
        _logger = logger;
    }

    public ILogger CreateLogger(string categoryName) =>
        new SerilogMicrosoftLogger(_logger.ForContext("SourceContext", categoryName));

    public void Dispose()
    {
    }

    private sealed class SerilogMicrosoftLogger : ILogger
    {
        private readonly Serilog.ILogger _logger;

        public SerilogMicrosoftLogger(Serilog.ILogger logger)
        {
            _logger = logger;
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);
            if (string.IsNullOrEmpty(message) && exception is null)
            {
                return;
            }

            _logger.Write(ToSerilogLevel(logLevel), exception, "{Message}", message);
        }

        private static LogEventLevel ToSerilogLevel(LogLevel logLevel) => logLevel switch
        {
            LogLevel.Trace => LogEventLevel.Verbose,
            LogLevel.Debug => LogEventLevel.Debug,
            LogLevel.Information => LogEventLevel.Information,
            LogLevel.Warning => LogEventLevel.Warning,
            LogLevel.Error => LogEventLevel.Error,
            LogLevel.Critical => LogEventLevel.Fatal,
            _ => LogEventLevel.Information,
        };
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}