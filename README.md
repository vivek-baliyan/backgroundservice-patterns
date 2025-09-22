# BackgroundService Production Patterns

Production-ready patterns for .NET 6+ BackgroundServices that prevent silent failures, resource exhaustion, and monitoring blind spots.

## Requirements

- .NET 6 or later (uses latest async patterns like `Task.WaitAsync()`)
- C# 10+ features (file-scoped namespaces, `required` properties)
- Compatible with .NET 8 LTS (recommended for production)

## Repository Structure

```
backgroundservice-patterns/
├── README.md
├── LICENSE
├── .gitignore
├── CONTRIBUTING.md
├── BackgroundServicePatterns.csproj
└── src/
    ├── Examples/
    │   ├── SilentFailureHandler.cs
    │   ├── ConcurrencyControl.cs
    │   ├── GracefulShutdown.cs
    │   ├── ScopeManagement.cs
    │   └── Monitoring.cs
    ├── Templates/
    │   ├── ProductionTemplate.cs
    │   └── BasicTemplate.cs
    └── HealthChecks/
        ├── BackgroundServiceHealthCheck.cs
        └── HealthCheckExtensions.cs
```

## Quick Start

**Modern .NET 6+ BackgroundService with latest patterns:**

```csharp
public class MyBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MyBackgroundService> _logger;
    private readonly SemaphoreSlim _concurrencyLimiter = new(10, 10);
    private DateTime _lastSuccessfulRun = DateTime.UtcNow;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                await ProcessBatch(scope.ServiceProvider, stoppingToken).ConfigureAwait(false);
                _lastSuccessfulRun = DateTime.UtcNow;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Processing failed, retrying in 30s");
                try
                {
                    await Task.Delay(30000, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
            
            try
            {
                await Task.Delay(5000, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
    
    public bool IsHealthy => DateTime.UtcNow - _lastSuccessfulRun < TimeSpan.FromMinutes(10);

    private async Task ProcessBatch(IServiceProvider services, CancellationToken stoppingToken)
    {
        // Replace with your actual business logic
        var workService = services.GetRequiredService<IWorkService>();
        var workItems = await workService.GetWorkItemsAsync(stoppingToken);

        foreach (var item in workItems)
        {
            try
            {
                await workService.ProcessWorkItemAsync(item.Id, stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process item {ItemId}", item.Id);
                // Continue processing other items
            }
        }
    }
}
```

## Patterns Covered

1. **Silent Failure Prevention** - Exception handling that keeps services running
2. **Concurrency Control** - Preventing resource exhaustion from unlimited parallelism  
3. **Graceful Shutdown** - Proper cancellation token handling for Kubernetes/cloud deployments
4. **Scope Management** - Avoiding memory leaks from captive dependencies
5. **Monitoring & Health Checks** - Making background services observable

## Getting Started

To use these patterns in your project:

1. Clone or download this repository
2. Copy the relevant pattern files to your project
3. Install required NuGet packages (see BackgroundServicePatterns.csproj)
4. Adapt the interfaces and models to your business logic

## Usage

Each pattern includes:
- ❌ **Problem code** - Shows the trap
- ✅ **Solution code** - Shows the fix  
- 🔧 **Production template** - Ready-to-use implementation
- 📊 **Monitoring integration** - Health checks and metrics

## Contributing

Found a pattern we missed? Open an issue or submit a PR with:
- Problem description
- Minimal reproduction
- Proposed solution
- Production considerations

## License

MIT - Use these patterns freely in your production systems.
