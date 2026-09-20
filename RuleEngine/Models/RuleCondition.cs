
using RuleEngine.Models.Enum;

namespace RuleEngine.Models;

public class RuleCondition
{
    public string TagName { get; set; } = string.Empty;
    public ComparisonOperator Operator { get; set; }
    public double Threshold { get; set; }
}
