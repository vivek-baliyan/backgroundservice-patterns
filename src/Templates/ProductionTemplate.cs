using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using BackgroundServicePatterns.Shared;

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

    // High-performance logging with LoggerMessage (latest .NET pattern)
    private static readonly Action<ILogger, Exception?> LogServiceStarted =
        LoggerMessage.Define(LogLevel.Information, new EventId(1, nameof(LogServiceStarted)), "Background service started");

    private static readonly Action<ILogger, long, Exception?> LogProcessingFailed =
        LoggerMessage.Define<long>(LogLevel.Error, new EventId(2, nameof(LogProcessingFailed)),
            "Processing failed, consecutive failures: {FailureCount}");

    private static readonly Action<ILogger, int, Exception?> LogBatchProcessing =
        LoggerMessage.Define<int>(LogLevel.Information, new EventId(3, nameof(LogBatchProcessing)),
            "Processing batch of {Count} payments");

    /// <summary>
    /// Initializes a new instance of the ProductionBackgroundServiceTemplate.
    /// </summary>
    public ProductionBackgroundServiceTemplate(
        IServiceScopeFactory scopeFactory,
        ILogger<ProductionBackgroundServiceTemplate> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Executes the background service with all production patterns implemented.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogServiceStarted(_logger, null);
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                await ProcessBatch(scope.ServiceProvider, stoppingToken).ConfigureAwait(false);
                
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
                LogProcessingFailed(_logger, _consecutiveFailures, ex);
                
                // Exponential backoff for repeated failures
                var delayMs = Math.Min(300000, 5000 * Math.Pow(2, Math.Min(_consecutiveFailures - 1, 6)));
                
                try
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(delayMs), stoppingToken).ConfigureAwait(false);
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

        LogBatchProcessing(_logger, payments.Count, null);
        
        var tasks = payments.Select(async payment =>
        {
            await _concurrencyLimiter.WaitAsync(stoppingToken).ConfigureAwait(false);
            try
            {
                stoppingToken.ThrowIfCancellationRequested();
                
                // Create scope per payment for proper resource management
                using var paymentScope = _scopeFactory.CreateScope();
                var scopedPaymentService = paymentScope.ServiceProvider.GetRequiredService<IPaymentService>();
                
                await scopedPaymentService.ProcessPaymentAsync(payment.Id, stoppingToken).ConfigureAwait(false);
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
        
        await Task.WhenAll(tasks).ConfigureAwait(false);
        _logger.LogInformation("Completed batch processing");
    }

    // Health check properties
    /// <summary>
    /// Gets the timestamp of the last successful processing run.
    /// </summary>
    public DateTime LastSuccessfulRun => _lastSuccessfulRun;
    /// <summary>
    /// Gets the number of consecutive failures that have occurred.
    /// </summary>
    public long ConsecutiveFailures => _consecutiveFailures;
    /// <summary>
    /// Gets a value indicating whether the service is currently healthy.
    /// </summary>
    public bool IsHealthy => DateTime.UtcNow - _lastSuccessfulRun < TimeSpan.FromMinutes(10) && _consecutiveFailures < 5;

    /// <summary>
    /// Disposes of the service resources.
    /// </summary>
    public override void Dispose()
    {
        _concurrencyLimiter?.Dispose();
        base.Dispose();
    }
}

// Extension method for easy registration
/// <summary>
/// Extension methods for registering background services.
/// </summary>
public static class BackgroundServiceExtensions
{
    /// <summary>
    /// Adds a production background service with proper singleton registration.
    /// </summary>
    public static IServiceCollection AddProductionBackgroundService<TService>(this IServiceCollection services)
        where TService : class, IHostedService
    {
        services.AddSingleton<TService>();
        services.AddHostedService(provider => provider.GetRequiredService<TService>());
        return services;
    }
}
