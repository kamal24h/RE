
using RuleEngine.Models;

namespace RuleEngine.Services;

public interface IRuleStore
{
    Task<IReadOnlyList<Rule>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Rule>> GetByDeviceAsync(string deviceId, CancellationToken ct = default);
    Task<Rule?> GetAsync(string id, CancellationToken ct = default);
    Task AddAsync(Rule rule, CancellationToken ct = default);
    Task UpdateAsync(Rule rule, CancellationToken ct = default);
    Task<bool> DeleteAsync(string id, CancellationToken ct = default);
}