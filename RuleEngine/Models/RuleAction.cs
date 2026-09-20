namespace RuleEngine.Models;

public class RuleAction
{
    public string Type { get; set; } = string.Empty; // Email, Webhook, Log, MqttPublish
    public Dictionary<string, string> Parameters { get; set; } = new();
}
