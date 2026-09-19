
using RuleEngine.Models;

namespace RuleEngine.Services.Actions;

public interface IRuleActionExecutor
{
    string ActionType { get; }
    Task ExecuteAsync(Rule rule, SensorReading reading, RuleAction action, CancellationToken ct);
}