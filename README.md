# BackgroundService Production Patterns

Production-ready patterns for .NET 8+ BackgroundServices that prevent silent failures, resource exhaustion, and monitoring blind spots.

## 🚀 Quick Start - Try It Now!

**Run the interactive demo to see all patterns in action:**

```bash
git clone https://github.com/your-org/backgroundservice-patterns.git
cd backgroundservice-patterns
dotnet run
```

Choose from 6 interactive demos showing real problems and their solutions. Perfect for learning!

## 📚 Start Here - Learning Path

**New to BackgroundServices?** Follow this order:

1. **🔥 Silent Failure Prevention** (`SilentFailureHandler.cs`) - Start here! Learn why services die silently
2. **🛑 Graceful Shutdown** (`GracefulShutdown.cs`) - Essential for cloud deployments
3. **🔧 Scope Management** (`ScopeManagement.cs`) - Prevent memory leaks and captive dependencies
4. **⚡ Concurrency Control** (`ConcurrencyControl.cs`) - Prevent resource exhaustion
5. **📊 Monitoring** (`Monitoring.cs`) - Add observability and health checks
6. **🏭 Production Template** (`ProductionTemplate.cs`) - Complete implementation

**Each file shows the ❌ problem and ✅ solution side by side!**

## Requirements

- .NET 8+ (uses latest async patterns and high-performance logging)
- C# 12+ features (file-scoped namespaces, `required` properties)
- Works in Docker, Kubernetes, and cloud environments

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

**Modern .NET 8+ BackgroundService with latest patterns:**

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

## 🎯 The 5 Critical Problems This Solves

### 1. 🔥 Silent Failures
**💥 Problem:** Service dies silently when exceptions aren't handled properly
**🚨 Real Impact:** Payment processing stops, no alerts, lost revenue
**✅ Solution:** Dual-layer exception handling with exponential backoff

### 2. ⚡ Resource Exhaustion
**💥 Problem:** Unlimited concurrency overwhelms database/API connections
**🚨 Real Impact:** System crashes under load, cascading failures
**✅ Solution:** SemaphoreSlim throttling or Channel-based backpressure

### 3. 🛑 Poor Shutdown Handling
**💥 Problem:** Services don't stop gracefully in Kubernetes/cloud environments
**🚨 Real Impact:** Data corruption, lost work, failed deployments
**✅ Solution:** Proper CancellationToken propagation and task tracking

### 4. 🔧 Memory Leaks
**💥 Problem:** Captive dependencies keep scoped services alive forever
**🚨 Real Impact:** Memory usage grows until container OOM kills
**✅ Solution:** Proper IServiceScopeFactory usage per operation

### 5. 📊 Monitoring Blind Spots
**💥 Problem:** No visibility into service health or performance
**🚨 Real Impact:** Silent degradation, impossible to troubleshoot
**✅ Solution:** Health checks, metrics, structured logging

## Getting Started

To use these patterns in your project:

1. Clone or download this repository
2. Copy the relevant pattern files to your project
3. Install required NuGet packages (see BackgroundServicePatterns.csproj)
4. Adapt the interfaces and models to your business logic

## 💡 How to Use This Repository

**For Learning:**
1. Run `dotnet run` for interactive demos
2. Follow the learning path above (start with Silent Failures)
3. Read the ❌ anti-patterns and ✅ solutions in each file
4. Try breaking the examples to see failures happen

**For Production:**
1. Copy `ProductionTemplate.cs` as your starting point
2. Adapt the interfaces to your business logic
3. Add your specific health check thresholds
4. Configure logging and monitoring for your environment

Each pattern includes:
- ❌ **Problem code** - Shows what NOT to do
- ✅ **Solution code** - Shows the correct implementation
- 🔧 **Production template** - Copy-paste ready implementation
- 📊 **Health monitoring** - Observability and alerting

## Contributing

Found a pattern we missed? Open an issue or submit a PR with:
- Problem description
- Minimal reproduction
- Proposed solution
- Production considerations

## License

MIT - Use these patterns freely in your production systems.
