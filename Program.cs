using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using BackgroundServicePatterns.Shared;
using BackgroundServicePatterns.Examples;
using BackgroundServicePatterns.Templates;

namespace BackgroundServicePatterns;

/// <summary>
/// Interactive demo showing all 5 BackgroundService patterns in action.
/// Run this to see the difference between problematic and production-ready implementations.
/// </summary>
public class Program
{
    /// <summary>
    /// Main entry point for the interactive demo application.
    /// </summary>
    /// <param name="args">Command line arguments</param>
    public static async Task Main(string[] args)
    {
        Console.WriteLine("🎯 BackgroundService Patterns Interactive Demo");
        Console.WriteLine("===============================================");
        Console.WriteLine();

        while (true)
        {
            ShowMenu();
            var choice = Console.ReadLine()?.ToLower();

            switch (choice)
            {
                case "1":
                    await RunSilentFailureDemo();
                    break;
                case "2":
                    await RunConcurrencyDemo();
                    break;
                case "3":
                    await RunShutdownDemo();
                    break;
                case "4":
                    await RunScopeDemo();
                    break;
                case "5":
                    await RunMonitoringDemo();
                    break;
                case "6":
                    await RunProductionTemplate();
                    break;
                case "q":
                    return;
                default:
                    Console.WriteLine("❌ Invalid choice. Try again.");
                    break;
            }

            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
            Console.Clear();
        }
    }

    private static void ShowMenu()
    {
        Console.WriteLine("Choose a pattern to demonstrate:");
        Console.WriteLine("1. 🔥 Silent Failure Prevention");
        Console.WriteLine("2. ⚡ Concurrency Control");
        Console.WriteLine("3. 🛑 Graceful Shutdown");
        Console.WriteLine("4. 🔧 Scope Management");
        Console.WriteLine("5. 📊 Monitoring & Health Checks");
        Console.WriteLine("6. 🏭 Complete Production Template");
        Console.WriteLine("Q. Quit");
        Console.Write("\nYour choice: ");
    }

    private static async Task RunSilentFailureDemo()
    {
        Console.WriteLine("🔥 SILENT FAILURE PREVENTION DEMO");
        Console.WriteLine("=================================");
        Console.WriteLine();
        Console.WriteLine("💡 Problem: BackgroundService dies silently when exceptions aren't handled properly.");
        Console.WriteLine("✅ Solution: Dual-layer exception handling with exponential backoff.");
        Console.WriteLine();

        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<IPaymentService, MockPaymentService>();
                services.AddHostedService<SilentFailureHandler>();
            })
            .ConfigureLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Information);
                logging.AddConsole();
            })
            .Build();

        Console.WriteLine("🚀 Starting service (will run for 10 seconds)...");

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        try
        {
            await host.RunAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("✅ Service stopped gracefully after timeout");
        }
    }

    private static async Task RunConcurrencyDemo()
    {
        Console.WriteLine("⚡ CONCURRENCY CONTROL DEMO");
        Console.WriteLine("==========================");
        Console.WriteLine();
        Console.WriteLine("💡 Problem: Unlimited concurrency can exhaust system resources.");
        Console.WriteLine("✅ Solution: SemaphoreSlim throttling or Channel-based backpressure.");
        Console.WriteLine();

        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<IPaymentService, MockPaymentService>();
                services.AddHostedService<ConcurrencyControlWithSemaphore>();
            })
            .ConfigureLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Information);
                logging.AddConsole();
            })
            .Build();

        Console.WriteLine("🚀 Starting service with concurrency limits (max 10 concurrent operations)...");

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        try
        {
            await host.RunAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("✅ Service stopped gracefully after timeout");
        }
    }

    private static async Task RunShutdownDemo()
    {
        Console.WriteLine("🛑 GRACEFUL SHUTDOWN DEMO");
        Console.WriteLine("=========================");
        Console.WriteLine();
        Console.WriteLine("💡 Problem: Services don't handle cancellation properly in cloud environments.");
        Console.WriteLine("✅ Solution: Proper CancellationToken propagation and task tracking.");
        Console.WriteLine();

        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<IPaymentService, MockPaymentService>();
                services.AddHostedService<GracefulShutdownExample>();
            })
            .ConfigureLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Information);
                logging.AddConsole();
            })
            .Build();

        Console.WriteLine("🚀 Starting service... Press Ctrl+C to test graceful shutdown");

        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            Console.WriteLine("\n🛑 Shutdown requested...");
        };

        try
        {
            await host.RunAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("✅ Service stopped gracefully");
        }
    }

    private static async Task RunScopeDemo()
    {
        Console.WriteLine("🔧 SCOPE MANAGEMENT DEMO");
        Console.WriteLine("========================");
        Console.WriteLine();
        Console.WriteLine("💡 Problem: Captive dependencies cause memory leaks in long-running services.");
        Console.WriteLine("✅ Solution: Proper IServiceScopeFactory usage with scope-per-operation.");
        Console.WriteLine();

        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddScoped<IPaymentService, MockPaymentService>();
                services.AddScoped<IPaymentDbContext, MockPaymentDbContext>();
                services.AddHostedService<ScopeManagementExample>();
            })
            .ConfigureLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Information);
                logging.AddConsole();
            })
            .Build();

        Console.WriteLine("🚀 Starting service with proper scope management...");

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(12));

        try
        {
            await host.RunAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("✅ Service stopped gracefully after timeout");
        }
    }

    private static async Task RunMonitoringDemo()
    {
        Console.WriteLine("📊 MONITORING & HEALTH CHECKS DEMO");
        Console.WriteLine("==================================");
        Console.WriteLine();
        Console.WriteLine("💡 Problem: No visibility into background service health and performance.");
        Console.WriteLine("✅ Solution: Comprehensive metrics, structured logging, and health checks.");
        Console.WriteLine();

        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<IPaymentService, MockPaymentService>();
                services.AddHostedService<MonitoringExample>();
                services.AddHealthChecks();
            })
            .ConfigureLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Information);
                logging.AddConsole();
            })
            .Build();

        Console.WriteLine("🚀 Starting service with full monitoring capabilities...");

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        try
        {
            await host.RunAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("✅ Service stopped gracefully after timeout");
        }
    }

    private static async Task RunProductionTemplate()
    {
        Console.WriteLine("🏭 PRODUCTION TEMPLATE DEMO");
        Console.WriteLine("===========================");
        Console.WriteLine();
        Console.WriteLine("🎯 Complete implementation combining all 5 patterns:");
        Console.WriteLine("   ✅ Silent failure prevention");
        Console.WriteLine("   ✅ Concurrency control");
        Console.WriteLine("   ✅ Graceful shutdown");
        Console.WriteLine("   ✅ Proper scope management");
        Console.WriteLine("   ✅ Health monitoring");
        Console.WriteLine();

        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddScoped<IPaymentService, MockPaymentService>();
                services.AddProductionBackgroundService<ProductionBackgroundServiceTemplate>();
                services.AddHealthChecks();
            })
            .ConfigureLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Information);
                logging.AddConsole();
            })
            .Build();

        Console.WriteLine("🚀 Starting production-ready service...");

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        try
        {
            await host.RunAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("✅ Production service stopped gracefully after timeout");
        }
    }
}

/// <summary>
/// Mock implementation for demo purposes - simulates payment processing with realistic delays and occasional failures.
/// </summary>
public class MockPaymentService : IPaymentService
{
    private readonly Random _random = new();
    private int _paymentIdCounter = 1;

    /// <summary>
    /// Gets a random collection of pending payments for demo purposes.
    /// </summary>
    public async Task<List<Payment>> GetPendingPaymentsAsync(CancellationToken cancellationToken = default)
    {
        await Task.Delay(100, cancellationToken); // Simulate DB query

        var count = _random.Next(1, 6); // 1-5 payments
        return Enumerable.Range(0, count)
            .Select(_ => new Payment
            {
                Id = _paymentIdCounter++,
                Status = "Pending",
                Amount = _random.Next(10, 1000),
                CreatedAt = DateTime.UtcNow
            })
            .ToList();
    }

    /// <summary>
    /// Gets a limited number of pending payments for demo purposes.
    /// </summary>
    public async Task<List<Payment>> GetPendingPaymentsAsync(int maxCount, CancellationToken cancellationToken = default)
    {
        var all = await GetPendingPaymentsAsync(cancellationToken);
        return all.Take(maxCount).ToList();
    }

    /// <summary>
    /// Processes a payment with simulated delays and occasional failures for demo purposes.
    /// </summary>
    public async Task ProcessPaymentAsync(int paymentId, CancellationToken cancellationToken = default)
    {
        // Simulate processing time
        await Task.Delay(_random.Next(50, 500), cancellationToken);

        // Simulate occasional failures (10% chance)
        if (_random.NextDouble() < 0.1)
        {
            throw new InvalidOperationException($"Payment {paymentId} failed due to insufficient funds");
        }

        // Success - payment processed
    }
}

/// <summary>
/// Mock database context for scope management demonstrations.
/// </summary>
public class MockPaymentDbContext : IPaymentDbContext
{
    /// <summary>
    /// Simulates saving changes to a database for demo purposes.
    /// </summary>
    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return Task.Delay(50, cancellationToken); // Simulate DB save
    }
}