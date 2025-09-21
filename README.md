# BackgroundService Production Patterns

Production-ready patterns for .NET BackgroundServices that prevent silent failures, resource exhaustion, and monitoring blind spots.

## Repository Structure

```
backgroundservice-patterns/
├── README.md
├── src/
│   ├── Examples/
│   │   ├── SilentFailureHandler.cs
│   │   ├── ConcurrencyControl.cs
│   │   ├── GracefulShutdown.cs
│   │   ├── ScopeManagement.cs
│   │   └── Monitoring.cs
│   ├── Templates/
│   │   ├── ProductionTemplate.cs
│   │   └── BasicTemplate.cs
│   └── HealthChecks/
│       ├── BackgroundServiceHealthCheck.cs
│       └── HealthCheckExtensions.cs
├── docs/
│   ├── patterns.md
│   ├── monitoring.md
│   └── deployment.md
└── samples/
    ├── PaymentProcessor/
    └── EmailProcessor/
```

## Quick Start

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
                await ProcessBatch(scope.ServiceProvider, stoppingToken);
                _lastSuccessfulRun = DateTime.UtcNow;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Processing failed, retrying in 30s");
                await Task.Delay(30000, stoppingToken);
            }
            
            await Task.Delay(5000, stoppingToken);
        }
    }
    
    public bool IsHealthy => DateTime.UtcNow - _lastSuccessfulRun < TimeSpan.FromMinutes(10);
}
```

## Patterns Covered

1. **Silent Failure Prevention** - Exception handling that keeps services running
2. **Concurrency Control** - Preventing resource exhaustion from unlimited parallelism  
3. **Graceful Shutdown** - Proper cancellation token handling for Kubernetes/cloud deployments
4. **Scope Management** - Avoiding memory leaks from captive dependencies
5. **Monitoring & Health Checks** - Making background services observable

## Related Article

[Why Our .NET BackgroundService Stalled — and How We Fixed It](https://medium.com/your-article-link)

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
