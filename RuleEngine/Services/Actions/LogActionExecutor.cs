
using RuleEngine.Models;

namespace RuleEngine.Services.Actions;

public class LogActionExecutor : IRuleActionExecutor
{
    private readonly ILogger<LogActionExecutor> _logger;
    public string ActionType => "Log";

    public LogActionExecutor(ILogger<LogActionExecutor> logger) => _logger = logger;

    public Task ExecuteAsync(Rule rule, SensorReading reading, RuleAction action, CancellationToken ct)
    {
        _logger.LogWarning(
            "[RULE TRIGGERED] Rule='{Rule}' Device='{Device}' Tag='{Tag}' Value={Value} Severity={Severity}",
            rule.Name, reading.DeviceId, reading.TagName, reading.Value, rule.Severity);
        return Task.CompletedTask;
    }
}