using Microsoft.Extensions.Logging;
using Serilog.Events;

namespace CliReader.Logging;

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
        private const string OriginalFormatPropertyName = "{OriginalFormat}";
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

            var serilogLevel = ToSerilogLevel(logLevel);
            if (state is IEnumerable<KeyValuePair<string, object?>> properties)
            {
                WriteStructured(serilogLevel, exception, properties, formatter(state, exception));
                return;
            }

            var message = formatter(state, exception);
            if (string.IsNullOrEmpty(message) && exception is null)
            {
                return;
            }

            _logger.Write(serilogLevel, exception, "{Message}", message);
        }

        private void WriteStructured(
            LogEventLevel logLevel,
            Exception? exception,
            IEnumerable<KeyValuePair<string, object?>> properties,
            string fallbackMessage)
        {
            var logger = _logger;
            var messageTemplate = fallbackMessage;

            foreach (var property in properties)
            {
                if (property.Key == OriginalFormatPropertyName)
                {
                    if (property.Value is string originalFormat)
                    {
                        messageTemplate = originalFormat;
                    }

                    continue;
                }

                logger = logger.ForContext(property.Key, property.Value, destructureObjects: false);
            }

            if (string.IsNullOrEmpty(messageTemplate) && exception is null)
            {
                return;
            }

            logger.Write(logLevel, exception, messageTemplate);
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