
using RuleEngine.Services;
using RuleEngine.Services.Actions;
using RuleEngine.Services.Ingestion;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Rule engine
builder.Services.AddSingleton<IRuleStore, InMemoryRuleStore>();
builder.Services.AddSingleton<IRuleEvaluator, RuleEvaluator>();
builder.Services.AddScoped<IRuleEngineService, RuleEngineService>();

// Action executors
builder.Services.AddSingleton<IRuleActionExecutor, LogActionExecutor>();
builder.Services.AddSingleton<IRuleActionExecutor, WebhookActionExecutor>();
builder.Services.AddHttpClient("RuleEngineWebhook", c => c.Timeout = TimeSpan.FromSeconds(10));

// === Ingestion pipeline ===
builder.Services.Configure<IngestionQueueOptions>(builder.Configuration.GetSection("IngestionQueue"));
builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection("IngestionWorker"));

builder.Services.AddSingleton<IIngestionQueue, ChannelIngestionQueue>();
builder.Services.AddHostedService<RuleEvaluationWorker>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();