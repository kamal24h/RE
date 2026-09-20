using RuleEngine.Models;
using Microsoft.Extensions.Options;

namespace RuleEngine.Services.Ingestion;

public sealed class RuleEvaluationWorker : BackgroundService
{
    private readonly IIngestionQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;   // <-- injected instead of IRuleEngineService
    private readonly WorkerOptions _options;
    private readonly ILogger<RuleEvaluationWorker> _logger;

    private long _processed;
    private long _failed;

    public RuleEvaluationWorker(
        IIngestionQueue queue,
        IServiceScopeFactory scopeFactory,
        IOptions<WorkerOptions> options,
        ILogger<RuleEvaluationWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
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

                while (batch.Count < _options.BatchSize && _queue.TryRead(out var extra))
                    batch.Add(extra);

                await ProcessBatchAsync(workerId, batch, ct);
                batch.Clear();
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Worker {WorkerId} crashed", workerId);
        }
    }

    private async Task ProcessBatchAsync(int workerId, List<SensorReading> batch, CancellationToken ct)
    {
        // One scope per batch — bounded memory, correct disposal of scoped deps.
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var engine = scope.ServiceProvider.GetRequiredService<IRuleEngineService>();

            await engine.ProcessBatchAsync(batch, ct);
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