
using RuleEngine.Models;
using System.Collections.Concurrent;

namespace RuleEngine.Services;

public class InMemoryRuleStore : IRuleStore
{
    private readonly ConcurrentDictionary<string, Rule> _rules = new();

    public Task<IReadOnlyList<Rule>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Rule>>(_rules.Values.ToList());

    public Task<IReadOnlyList<Rule>> GetByDeviceAsync(string deviceId, CancellationToken ct = default)
    {
        var matches = _rules.Values
            .Where(r => r.IsEnabled && (r.DeviceId == "*" || r.DeviceId == deviceId))
            .ToList();
        return Task.FromResult<IReadOnlyList<Rule>>(matches);
    }

    public Task<Rule?> GetAsync(string id, CancellationToken ct = default)
        => Task.FromResult(_rules.TryGetValue(id, out var r) ? r : null);

    public Task AddAsync(Rule rule, CancellationToken ct = default)
    {
        _rules[rule.Id] = rule;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Rule rule, CancellationToken ct = default)
    {
        _rules[rule.Id] = rule;
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(string id, CancellationToken ct = default)
        => Task.FromResult(_rules.TryRemove(id, out _));
}