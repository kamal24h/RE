
using Microsoft.Extensions.Options;
using RuleEngine.Models;

namespace RuleEngine.Services.Ingestion;

public class WorkerOptions
{
    /// <summary>Number of parallel consumers draining the channel.</summary>
    public int WorkerCount { get; set; } = 4;

    /// <summary>Readings pulled per iteration into a micro-batch.</summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>Channel capacity used for batching.</summary>
    public int BatchChannelCapacity { get; set; } = 1000;
}

public sealed class RuleEvaluationWorker : BackgroundService
{
    private readonly IIngestionQueue _queue;
    private readonly IRuleEngineService _engine;
    private readonly WorkerOptions _options;
    private readonly ILogger<RuleEvaluationWorker> _logger;

    private long _processed;
    private long _failed;

    public RuleEvaluationWorker(
        IIngestionQueue queue,
        IRuleEngineService engine,
        IOptions<WorkerOptions> options,
        ILogger<RuleEvaluationWorker> logger)
    {
        _queue = queue;
        _engine = engine;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "RuleEvaluationWorker starting with {Count} consumers, batch={Batch}",
            _options.WorkerCount, _options.BatchSize);

        var consumers = Enumerable.Range(0, _options.WorkerCount)
            .Select(i => ConsumeAsync(i, stoppingToken))
            .ToArray();

        await Task.WhenAll(consumers);

        _logger.LogInformation(
            "RuleEvaluationWorker stopped. Processed={Processed} Failed={Failed}",
            _processed, _failed);
    }

    private async Task ConsumeAsync(int workerId, CancellationToken ct)
    {
        var batch = new List<SensorReading>(_options.BatchSize);

        try
        {
            await foreach (var reading in _queue.ReadAllAsync(ct))
            {
                batch.Add(reading);

                // Non-blocking drain: greedily pull up to BatchSize before evaluating.
                while (batch.Count < _options.BatchSize &&
                       _queue.TryRead(out var extra))
                {
                    batch.Add(extra);
                }

                await ProcessBatchAsync(workerId, batch, ct);
                batch.Clear();
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // graceful shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Worker {WorkerId} crashed", workerId);
        }
    }

    private async Task ProcessBatchAsync(int workerId, List<SensorReading> batch, CancellationToken ct)
    {
        try
        {
            await _engine.ProcessBatchAsync(batch, ct);
            Interlocked.Add(ref _processed, batch.Count);
        }
        catch (Exception ex)
        {
            Interlocked.Add(ref _failed, batch.Count);
            _logger.LogError(ex, "Worker {WorkerId} failed to process batch of {Count}", workerId, batch.Count);
        }
    }

    public IngestionMetrics Snapshot() => new(
        Processed: Interlocked.Read(ref _processed),
        Failed: Interlocked.Read(ref _failed),
        QueueDepth: _queue.Count);

    public record IngestionMetrics(long Processed, long Failed, int QueueDepth);
}