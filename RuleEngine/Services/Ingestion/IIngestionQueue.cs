
using RuleEngine.Models;

namespace RuleEngine.Services.Ingestion;

public interface IIngestionQueue
{
    /// <summary>
    /// Try to enqueue a reading without blocking.
    /// Returns false if the bounded channel is full (backpressure signal).
    /// </summary>
    ValueTask<bool> TryEnqueueAsync(SensorReading reading, CancellationToken ct = default);

    /// <summary>
    /// Enqueue with backpressure — awaits until space is available.
    /// </summary>
    ValueTask EnqueueAsync(SensorReading reading, CancellationToken ct = default);

    /// <summary>
    /// Reader side — consumed by background workers.
    /// </summary>
    IAsyncEnumerable<SensorReading> ReadAllAsync(CancellationToken ct = default);

    bool TryRead(out SensorReading reading);

    int Count { get; }
}