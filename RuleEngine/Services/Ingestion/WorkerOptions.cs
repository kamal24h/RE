// Services/Ingestion/WorkerOptions.cs
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
