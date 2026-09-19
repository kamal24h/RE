
namespace RuleEngine.Models;

public enum RuleSeverity { Info, Warning, Critical }

public enum ComparisonOperator
{
    Equals, NotEquals, GreaterThan, GreaterThanOrEqual,
    LessThan, LessThanOrEqual
}

public enum LogicalOperator { And, Or }

public class RuleCondition
{
    public string TagName { get; set; } = string.Empty;
    public ComparisonOperator Operator { get; set; }
    public double Threshold { get; set; }
}

public class RuleAction
{
    public string Type { get; set; } = string.Empty; // Email, Webhook, Log, MqttPublish
    public Dictionary<string, string> Parameters { get; set; } = new();
}

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