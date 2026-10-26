using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace PaymentGateway.Api.Tests.TestSupport;

internal sealed class RecordingLoggerProvider : ILoggerProvider
{
    public ConcurrentQueue<string> Messages { get; } = new();
    public ILogger CreateLogger(string categoryName) => new RecordingLogger(Messages);
    public void Dispose() { }

    private sealed class RecordingLogger(ConcurrentQueue<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => messages.Enqueue(formatter(state, exception) + exception);
    }
}
