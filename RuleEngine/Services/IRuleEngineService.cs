

using RuleEngine.Models;

namespace RuleEngine.Services;

public interface IRuleEngineService
{
    Task<IReadOnlyList<RuleExecutionResult>> ProcessReadingAsync(SensorReading reading, CancellationToken ct = default);
    Task<IReadOnlyList<RuleExecutionResult>> ProcessBatchAsync(IEnumerable<SensorReading> readings, CancellationToken ct = default);
}