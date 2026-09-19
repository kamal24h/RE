
using RuleEngine.Models;

namespace RuleEngine.Services;

public interface IRuleEvaluator
{
    bool Evaluate(Rule rule, SensorReading reading);
    bool EvaluateCondition(RuleCondition condition, SensorReading reading);
}