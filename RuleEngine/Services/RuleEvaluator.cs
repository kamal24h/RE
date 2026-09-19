
using RuleEngine.Models;

namespace RuleEngine.Services;

public class RuleEvaluator : IRuleEvaluator
{
    private readonly ILogger<RuleEvaluator> _logger;

    public RuleEvaluator(ILogger<RuleEvaluator> logger) => _logger = logger;

    public bool Evaluate(Rule rule, SensorReading reading)
    {
        if (rule.Conditions.Count == 0) return false;

        var results = rule.Conditions.Select(c => EvaluateCondition(c, reading)).ToList();

        return rule.LogicalOperator == LogicalOperator.And
            ? results.All(r => r)
            : results.Any(r => r);
    }

    public bool EvaluateCondition(RuleCondition condition, SensorReading reading)
    {
        if (!string.Equals(condition.TagName, reading.TagName, StringComparison.OrdinalIgnoreCase))
            return false;

        var v = reading.Value;
        var t = condition.Threshold;

        return condition.Operator switch
        {
            ComparisonOperator.Equals => Math.Abs(v - t) < double.Epsilon,
            ComparisonOperator.NotEquals => Math.Abs(v - t) >= double.Epsilon,
            ComparisonOperator.GreaterThan => v > t,
            ComparisonOperator.GreaterThanOrEqual => v >= t,
            ComparisonOperator.LessThan => v < t,
            ComparisonOperator.LessThanOrEqual => v <= t,
            _ => false
        };
    }
}