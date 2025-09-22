using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using BackgroundServicePatterns.Shared;

namespace BackgroundServicePatterns.Templates;

/// <summary>
/// Basic BackgroundService template with essential patterns.
/// Use this as a starting point for simpler scenarios.
/// For production systems, prefer ProductionBackgroundServiceTemplate.
/// </summary>
public class BasicBackgroundServiceTemplate : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BasicBackgroundServiceTemplate> _logger;
    private DateTime _lastSuccessfulRun = DateTime.UtcNow;

    public BasicBackgroundServiceTemplate(
        IServiceScopeFactory scopeFactory,
        ILogger<BasicBackgroundServiceTemplate> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Basic background service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Pattern 1: Proper scope management
                using var scope = _scopeFactory.CreateScope();
                await ProcessWork(scope.ServiceProvider, stoppingToken);

                // Pattern 2: Track successful runs for health checks
                _lastSuccessfulRun = DateTime.UtcNow;
            }
            catch (OperationCanceledException)
            {
                // Pattern 3: Graceful shutdown handling
                _logger.LogInformation("Shutdown requested");
                break;
            }
            catch (Exception ex)
            {
                // Pattern 4: Silent failure prevention
                _logger.LogError(ex, "Processing failed, retrying in 30 seconds");

                try
                {
                    await Task.Delay(30000, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            // Normal processing delay
            try
            {
                await Task.Delay(5000, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Basic background service stopped");
    }

    private async Task ProcessWork(IServiceProvider services, CancellationToken stoppingToken)
    {
        // Replace this with your actual business logic
        var paymentService = services.GetRequiredService<IPaymentService>();
        var payments = await paymentService.GetPendingPaymentsAsync(stoppingToken);

        foreach (var payment in payments)
        {
            try
            {
                stoppingToken.ThrowIfCancellationRequested();
                await paymentService.ProcessPaymentAsync(payment.Id, stoppingToken);

                _logger.LogDebug("Processed payment {PaymentId}", payment.Id);
            }
            catch (OperationCanceledException)
            {
                throw; // Re-throw cancellation
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process payment {PaymentId}", payment.Id);
                // Continue processing other items
            }
        }
    }

    // Basic health check property
    public bool IsHealthy => DateTime.UtcNow - _lastSuccessfulRun < TimeSpan.FromMinutes(10);
}

/// <summary>
/// Extension method for easy registration
/// </summary>
public static class BasicBackgroundServiceExtensions
{
    public static IServiceCollection AddBasicBackgroundService<TService>(this IServiceCollection services)
        where TService : class, IHostedService
    {
        services.AddSingleton<TService>();
        services.AddHostedService(provider => provider.GetRequiredService<TService>());
        return services;
    }
}