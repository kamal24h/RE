
using RuleEngine.Models;
using System.Text;
using System.Text.Json;

namespace RuleEngine.Services.Actions;

public class WebhookActionExecutor : IRuleActionExecutor
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebhookActionExecutor> _logger;
    public string ActionType => "Webhook";

    public WebhookActionExecutor(IHttpClientFactory httpClientFactory, ILogger<WebhookActionExecutor> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task ExecuteAsync(Rule rule, SensorReading reading, RuleAction action, CancellationToken ct)
    {
        if (!action.Parameters.TryGetValue("url", out var url) || string.IsNullOrWhiteSpace(url))
        {
            _logger.LogWarning("Webhook action missing 'url' parameter for rule {Rule}", rule.Id);
            return;
        }

        var payload = new
        {
            ruleId = rule.Id,
            ruleName = rule.Name,
            severity = rule.Severity.ToString(),
            deviceId = reading.DeviceId,
            tagName = reading.TagName,
            value = reading.Value,
            timestamp = reading.Timestamp
        };

        try
        {
            var client = _httpClientFactory.CreateClient("RuleEngineWebhook");
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await client.PostAsync(url, content, ct);
            if (!response.IsSuccessStatusCode)
                _logger.LogWarning("Webhook returned {Status} for rule {Rule}", response.StatusCode, rule.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Webhook execution failed for rule {Rule}", rule.Id);
        }
    }
}