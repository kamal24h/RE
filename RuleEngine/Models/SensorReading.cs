
namespace RuleEngine.Models;

public class SensorReading
{
    public string DeviceId { get; set; } = string.Empty;
    public string TagName { get; set; } = string.Empty;
    public double Value { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? Quality { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}