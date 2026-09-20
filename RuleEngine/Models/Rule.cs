
using RuleEngine.Models.Enum;

namespace RuleEngine.Models;

public class Rule
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string DeviceId { get; set; } = string.Empty; // "*" = any device
    public bool IsEnabled { get; set; } = true;
    public LogicalOperator LogicalOperator { get; set; } = LogicalOperator.And;
    public List<RuleCondition> Conditions { get; set; } = new();
    public List<RuleAction> Actions { get; set; } = new();
    public RuleSeverity Severity { get; set; } = RuleSeverity.Warning;
    public TimeSpan Cooldown { get; set; } = TimeSpan.FromMinutes(1);
    public DateTime? LastTriggeredUtc { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}