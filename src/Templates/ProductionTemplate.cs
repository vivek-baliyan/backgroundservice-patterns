using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BackgroundServicePatterns.Templates;

/// <summary>
/// Production-ready BackgroundService template that incorporates all 5 patterns:
/// 1. Silent failure prevention (exception handling)
/// 2. Concurrency control (SemaphoreSlim)
/// 3. Graceful shutdown (CancellationToken propagation)
/// 4. Proper scope management (IServiceScopeFactory)
/// 5. Basic monitoring (health tracking)
/// </summary>
public class ProductionBackgroundServiceTemplate : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProductionBackgroundServiceTemplate> _logger;
    private readonly SemaphoreSlim _concurrencyLimiter = new(10, 10);
    private DateTime _lastSuccessfulRun = DateTime.UtcNow;
    private long _consecutiveFailures = 0;

    public ProductionBackgroundServiceTemplate(
        IServiceScopeFactory scopeFactory,
        ILogger<ProductionBackgroundServiceTemplate> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Background service started");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                await ProcessBatch(scope.ServiceProvider, stoppingToken);
                
                _lastSuccessfulRun = DateTime.UtcNow;
                _consecutiveFailures = 0;
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Shutdown requested");
                break;
            }
            catch (Exception ex)
            {
                _consecutiveFailures++;
                _logger.LogError(ex, "Processing failed, consecutive failures: {FailureCount}", _consecutiveFailures);
                
                // Exponential backoff for repeated failures
                var delayMs = Math.Min(300000, 5000 * Math.Pow(2, Math.Min(_consecutiveFailures - 1, 6)));
                
                try
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(delayMs), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
            
            try
            {
                await Task.Delay(5000, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
        
        _logger.LogInformation("Background service stopped gracefully");
    }
    
    private async Task ProcessBatch(IServiceProvider services, CancellationToken stoppingToken)
    {
        // Example: process 50 payments with concurrency controls and proper error handling
        var paymentService = services.GetRequiredService<IPaymentService>();
        var payments = await paymentService.GetPendingPaymentsAsync(50, stoppingToken);
        
        if (!payments.Any())
        {
            _logger.LogDebug("No payments to process");
            return;
        }

        _logger.LogInformation("Processing batch of {Count} payments", payments.Count);
        
        var tasks = payments.Select(async payment =>
        {
            await _concurrencyLimiter.WaitAsync(stoppingToken);
            try
            {
                stoppingToken.ThrowIfCancellationRequested();
                
                // Create scope per payment for proper resource management
                using var paymentScope = _scopeFactory.CreateScope();
                var scopedPaymentService = paymentScope.ServiceProvider.GetRequiredService<IPaymentService>();
                
                await scopedPaymentService.ProcessPaymentAsync(payment.Id, stoppingToken);
                _logger.LogDebug("Processed payment {PaymentId}", payment.Id);
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Payment {PaymentId} processing cancelled", payment.Id);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process payment {PaymentId}", payment.Id);
                // Don't rethrow - continue processing other payments
            }
            finally
            {
                _concurrencyLimiter.Release();
            }
        });
        
        await Task.WhenAll(tasks);
        _logger.LogInformation("Completed batch processing");
    }

    // Health check properties
    public DateTime LastSuccessfulRun => _lastSuccessfulRun;
    public long ConsecutiveFailures => _consecutiveFailures;
    public bool IsHealthy => DateTime.UtcNow - _lastSuccessfulRun < TimeSpan.FromMinutes(10) && _consecutiveFailures < 5;

    public override void Dispose()
    {
        _concurrencyLimiter?.Dispose();
        base.Dispose();
    }
}

// Extension method for easy registration
public static class BackgroundServiceExtensions
{
    public static IServiceCollection AddProductionBackgroundService<TService>(this IServiceCollection services)
        where TService : class, IHostedService
    {
        services.AddSingleton<TService>();
        services.AddHostedService(provider => provider.GetRequiredService<TService>());
        return services;
    }
}

// Supporting interface
public interface IPaymentService
{
    Task<List<Payment>> GetPendingPaymentsAsync(CancellationToken cancellationToken = default);
    Task<List<Payment>> GetPendingPaymentsAsync(int maxCount, CancellationToken cancellationToken = default);
    Task ProcessPaymentAsync(int paymentId, CancellationToken cancellationToken = default);
}

public class Payment
{
    public int Id { get; set; }
    public string Status { get; set; } = "";
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; }
}
