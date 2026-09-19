
namespace RuleEngine.Models;

public class RuleExecutionResult
{
    public string RuleId { get; set; } = string.Empty;
    public string RuleName { get; set; } = string.Empty;
    public bool Triggered { get; set; }
    public bool SkippedByCooldown { get; set; }
    public SensorReading? Reading { get; set; }
    public RuleSeverity Severity { get; set; }
    public string? Message { get; set; }
    public DateTime EvaluatedUtc { get; set; } = DateTime.UtcNow;
}