using Microsoft.AspNetCore.Mvc;
using RuleEngine.Models;
using RuleEngine.Services;
using RuleEngine.Services.Ingestion;

namespace RuleEngine.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RuleEngineController : ControllerBase
    {
        private readonly IRuleEngineService _engine;
        private readonly IRuleStore _store;
        private readonly IIngestionQueue _queue;
        private readonly ILogger<RuleEngineController> _logger;

        public RuleEngineController(
            IRuleEngineService engine,
            IRuleStore store,
            IIngestionQueue queue,
            ILogger<RuleEngineController> logger)
        {
            _engine = engine;
            _store = store;
            _queue = queue;
            _logger = logger;
        }

        // ---------- INGESTION ----------

        /// <summary>
        /// Fire-and-forget ingestion. Returns 202 Accepted immediately;
        /// rule evaluation happens asynchronously in the worker pool.
        /// </summary>
        [HttpPost("ingest")]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> Ingest([FromBody] SensorReading reading, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(reading.DeviceId) || string.IsNullOrWhiteSpace(reading.TagName))
                return BadRequest(new { error = "DeviceId and TagName are required." });

            var accepted = await _queue.TryEnqueueAsync(reading, ct);
            if (!accepted)
            {
                // Bounded queue full — signal upstream to throttle
                return StatusCode(StatusCodes.Status429TooManyRequests,
                    new { error = "Ingestion queue saturated. Retry with backoff." });
            }

            return Accepted(new { queued = true, reading.DeviceId, reading.TagName });
        }

        /// <summary>
        /// Batch ingestion — enqueues all readings and returns 202.
        /// </summary>
        [HttpPost("ingest/batch")]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        public async Task<IActionResult> IngestBatch([FromBody] List<SensorReading> readings, CancellationToken ct)
        {
            if (readings is null || readings.Count == 0)
                return BadRequest(new { error = "At least one reading is required." });

            if (readings.Count > 5000)
                return BadRequest(new { error = "Batch size cannot exceed 5000 readings." });

            int accepted = 0, rejected = 0;
            foreach (var reading in readings)
            {
                if (string.IsNullOrWhiteSpace(reading.DeviceId) || string.IsNullOrWhiteSpace(reading.TagName))
                {
                    rejected++;
                    continue;
                }

                if (await _queue.TryEnqueueAsync(reading, ct))
                    accepted++;
                else
                    rejected++;
            }

            return Accepted(new { accepted, rejected, queueDepth = _queue.Count });
        }

        /// <summary>
        /// Synchronous evaluation path — bypasses the channel.
        /// Use for interactive diagnostics, not for high-throughput streams.
        /// </summary>
        [HttpPost("ingest/sync")]
        public async Task<IActionResult> IngestSync([FromBody] SensorReading reading, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(reading.DeviceId) || string.IsNullOrWhiteSpace(reading.TagName))
                return BadRequest(new { error = "DeviceId and TagName are required." });

            var results = await _engine.ProcessReadingAsync(reading, ct);
            return Ok(results);
        }

        [HttpGet("ingest/stats")]
        public IActionResult GetIngestionStats()
        {
            var worker = HttpContext.RequestServices
                .GetServices<IHostedService>()
                .OfType<RuleEvaluationWorker>()
                .FirstOrDefault();

            if (worker is null) return Ok(new { queueDepth = _queue.Count });
            return Ok(worker.Snapshot());
        }

        // ---------- RULE CRUD ----------

        /// <summary>List all registered rules.</summary>
        [HttpGet("rules")]
        public async Task<IActionResult> GetRules(CancellationToken ct)
            => Ok(await _store.GetAllAsync(ct));

        /// <summary>Get a single rule by id.</summary>
        [HttpGet("rules/{id}")]
        public async Task<IActionResult> GetRule(string id, CancellationToken ct)
        {
            var rule = await _store.GetAsync(id, ct);
            return rule is null ? NotFound() : Ok(rule);
        }

        /// <summary>Register a new rule.</summary>
        [HttpPost("rules")]
        public async Task<IActionResult> CreateRule([FromBody] Rule rule, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(rule.Name))
                return BadRequest(new { error = "Rule name is required." });

            if (rule.Conditions.Count == 0)
                return BadRequest(new { error = "At least one condition is required." });

            rule.Id = Guid.NewGuid().ToString();
            rule.CreatedUtc = DateTime.UtcNow;
            await _store.AddAsync(rule, ct);
            return CreatedAtAction(nameof(GetRule), new { id = rule.Id }, rule);
        }

        /// <summary>Update an existing rule.</summary>
        [HttpPut("rules/{id}")]
        public async Task<IActionResult> UpdateRule(string id, [FromBody] Rule rule, CancellationToken ct)
        {
            var existing = await _store.GetAsync(id, ct);
            if (existing is null) return NotFound();

            rule.Id = id;
            rule.CreatedUtc = existing.CreatedUtc;
            rule.LastTriggeredUtc = existing.LastTriggeredUtc;
            await _store.UpdateAsync(rule, ct);
            return Ok(rule);
        }

        /// <summary>Delete a rule.</summary>
        [HttpDelete("rules/{id}")]
        public async Task<IActionResult> DeleteRule(string id, CancellationToken ct)
            => await _store.DeleteAsync(id, ct) ? NoContent() : NotFound();

        /// <summary>Enable or disable a rule.</summary>
        [HttpPatch("rules/{id}/enabled")]
        public async Task<IActionResult> SetEnabled(string id, [FromQuery] bool enabled, CancellationToken ct)
        {
            var rule = await _store.GetAsync(id, ct);
            if (rule is null) return NotFound();

            rule.IsEnabled = enabled;
            await _store.UpdateAsync(rule, ct);
            return Ok(rule);
        }

        // ---------- AD-HOC EVALUATION ----------

        /// <summary>Dry-run a rule against a reading (no actions executed, no cooldown set).</summary>
        [HttpPost("rules/{id}/evaluate")]
        public async Task<IActionResult> EvaluateRule(string id, [FromBody] SensorReading reading, CancellationToken ct)
        {
            var rule = await _store.GetAsync(id, ct);
            if (rule is null) return NotFound();

            var evaluator = HttpContext.RequestServices.GetRequiredService<IRuleEvaluator>();
            var triggered = evaluator.Evaluate(rule, reading);

            return Ok(new
            {
                ruleId = rule.Id,
                ruleName = rule.Name,
                triggered,
                reading
            });
        }
    }
}