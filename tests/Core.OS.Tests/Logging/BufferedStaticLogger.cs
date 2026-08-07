using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;
using Serilog.Extensions.Logging;

namespace Core.OS.Tests.Logging;

/// <summary>
/// Installs a static Serilog logger whose sink buffers every event until the logger is closed,
/// mirroring the batching OpenTelemetry sink used in production. Events therefore only appear in
/// <see cref="Delivered"/> once the code under test flushes the static logger.
/// </summary>
internal sealed class BufferedStaticLogger : IDisposable
{
    private readonly Serilog.ILogger _previousLogger = Log.Logger;
    private readonly BufferingSink _sink = new();

    public IReadOnlyCollection<string> Delivered => _sink.Delivered;
    public ILoggerFactory LoggerFactory { get; }

    public BufferedStaticLogger()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(_sink, new BatchingOptions
            {
                BufferingTimeLimit = TimeSpan.FromMinutes(5),
                EagerlyEmitFirstEvent = false,
            })
            .CreateLogger();

        // as in Program.cs: loggers created by this factory bind to the static logger set above
        LoggerFactory = new SerilogLoggerFactory(logger: null, dispose: false);
    }

    public void Dispose()
    {
        LoggerFactory.Dispose();
        Log.Logger = _previousLogger;
    }

    private sealed class BufferingSink : IBatchedLogEventSink
    {
        public ConcurrentQueue<string> Delivered { get; } = new();

        public Task EmitBatchAsync(IReadOnlyCollection<LogEvent> batch)
        {
            foreach (var logEvent in batch)
                Delivered.Enqueue(logEvent.RenderMessage(CultureInfo.InvariantCulture));

            return Task.CompletedTask;
        }

        public Task OnEmptyBatchAsync() => Task.CompletedTask;
    }
}
