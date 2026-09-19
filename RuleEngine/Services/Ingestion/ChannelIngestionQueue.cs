
using Microsoft.Extensions.Options;
using RuleEngine.Models;
using System.Threading.Channels;

namespace RuleEngine.Services.Ingestion;

//FullMode guidance
//DropOldest → best for live telemetry where the newest reading matters most(SCADA dashboards).
//Wait → best when you must not lose data(alarm-only tags) — but this propagates backpressure to the HTTP caller.

public class IngestionQueueOptions
{
    /// <summary>Max buffered readings before backpressure kicks in.</summary>
    public int Capacity { get; set; } = 50_000;

    /// <summary>
    /// DropOldest keeps latency bounded for telemetry.
    /// Wait applies backpressure upstream (blocks producer).
    /// </summary>
    public BoundedChannelFullMode FullMode { get; set; } = BoundedChannelFullMode.DropOldest;
}

public sealed class ChannelIngestionQueue : IIngestionQueue
{
    private readonly Channel<SensorReading> _channel;
    private readonly ILogger<ChannelIngestionQueue> _logger;
    private long _droppedCount;

    public ChannelIngestionQueue(
        IOptions<IngestionQueueOptions> options,
        ILogger<ChannelIngestionQueue> logger)
    {
        _logger = logger;
        var opts = options.Value;

        _channel = Channel.CreateBounded<SensorReading>(new BoundedChannelOptions(opts.Capacity)
        {
            FullMode = opts.FullMode,
            SingleReader = false,   // multiple workers
            SingleWriter = false,   // many producers
            AllowSynchronousContinuations = false
        });
    }

    public int Count => _channel.Reader.Count;

    public ValueTask<bool> TryEnqueueAsync(SensorReading reading, CancellationToken ct = default)
    {
        if (_channel.Writer.TryWrite(reading))
            return ValueTask.FromResult(true);

        Interlocked.Increment(ref _droppedCount);
        if (Volatile.Read(ref _droppedCount) % 1000 == 0)
            _logger.LogWarning("Ingestion queue dropped {Count} readings due to backpressure", _droppedCount);

        return ValueTask.FromResult(false);
    }

    public ValueTask EnqueueAsync(SensorReading reading, CancellationToken ct = default)
        => _channel.Writer.WriteAsync(reading, ct);

    public IAsyncEnumerable<SensorReading> ReadAllAsync(CancellationToken ct = default)
        => _channel.Reader.ReadAllAsync(ct);

    public bool TryRead(out SensorReading reading) => _channel.Reader.TryRead(out reading!);
}