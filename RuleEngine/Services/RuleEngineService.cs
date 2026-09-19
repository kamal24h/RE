
using RuleEngine.Models;
using RuleEngine.Services.Actions;

namespace RuleEngine.Services;

public class RuleEngineService : IRuleEngineService
{
    private readonly IRuleStore _store;
    private readonly IRuleEvaluator _evaluator;
    private readonly IEnumerable<IRuleActionExecutor> _actionExecutors;
    private readonly ILogger<RuleEngineService> _logger;
    private static readonly SemaphoreSlim _cooldownLock = new(1, 1);

    public RuleEngineService(
        IRuleStore store,
        IRuleEvaluator evaluator,
        IEnumerable<IRuleActionExecutor> actionExecutors,
        ILogger<RuleEngineService> logger)
    {
        _store = store;
        _evaluator = evaluator;
        _actionExecutors = actionExecutors;
        _logger = logger;
    }

    public async Task<IReadOnlyList<RuleExecutionResult>> ProcessReadingAsync(
        SensorReading reading, CancellationToken ct = default)
    {
        var results = new List<RuleExecutionResult>();
        var rules = await _store.GetByDeviceAsync(reading.DeviceId, ct);

        foreach (var rule in rules)
        {
            var result = new RuleExecutionResult
            {
                RuleId = rule.Id,
                RuleName = rule.Name,
                Reading = reading,
                Severity = rule.Severity
            };

            try
            {
                if (!_evaluator.Evaluate(rule, reading))
                {
                    result.Triggered = false;
                    results.Add(result);
                    continue;
                }

                // Cooldown check
                if (rule.LastTriggeredUtc.HasValue &&
                    DateTime.UtcNow - rule.LastTriggeredUtc.Value < rule.Cooldown)
                {
                    result.Triggered = true;
                    result.SkippedByCooldown = true;
                    result.Message = $"Cooldown active until {rule.LastTriggeredUtc.Value + rule.Cooldown:O}";
                    results.Add(result);
                    continue;
                }

                result.Triggered = true;
                result.Message = $"Rule '{rule.Name}' triggered for {reading.TagName}={reading.Value}";
                rule.LastTriggeredUtc = DateTime.UtcNow;

                await ExecuteActionsAsync(rule, reading, ct);
                results.Add(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating rule {RuleId}", rule.Id);
                result.Message = $"Evaluation error: {ex.Message}";
                results.Add(result);
            }
        }

        return results;
    }

    public async Task<IReadOnlyList<RuleExecutionResult>> ProcessBatchAsync(
        IEnumerable<SensorReading> readings, CancellationToken ct = default)
    {
        var all = new List<RuleExecutionResult>();
        foreach (var reading in readings)
            all.AddRange(await ProcessReadingAsync(reading, ct));
        return all;
    }

    private async Task ExecuteActionsAsync(Rule rule, SensorReading reading, CancellationToken ct)
    {
        foreach (var action in rule.Actions)
        {
            var executor = _actionExecutors.FirstOrDefault(
                e => string.Equals(e.ActionType, action.Type, StringComparison.OrdinalIgnoreCase));

            if (executor == null)
            {
                _logger.LogWarning("No executor registered for action type {Type}", action.Type);
                continue;
            }

            try
            {
                await executor.ExecuteAsync(rule, reading, action, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Action {Type} failed for rule {Rule}", action.Type, rule.Id);
            }
        }
    }
}