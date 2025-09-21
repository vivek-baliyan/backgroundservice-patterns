using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BackgroundServicePatterns.Examples;

/// <summary>
/// Demonstrates proper exception handling to prevent silent failures.
/// The key pattern: wrap both individual operations AND the entire loop.
/// </summary>
public class SilentFailureHandler : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SilentFailureHandler> _logger;
    private DateTime _lastSuccessfulRun = DateTime.UtcNow;
    private long _consecutiveFailures = 0;

    public SilentFailureHandler(
        IServiceScopeFactory scopeFactory,
        ILogger<SilentFailureHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Payment processor started");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();
                
                var pendingPayments = await paymentService.GetPendingPaymentsAsync(stoppingToken);
                
                foreach (var payment in pendingPayments)
                {
                    try
                    {
                        await paymentService.ProcessPaymentAsync(payment.Id, stoppingToken);
                        _logger.LogDebug("Processed payment {PaymentId}", payment.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to process payment {PaymentId}", payment.Id);
                        // Continue processing other payments
                    }
                }
                
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
                _logger.LogCritical(ex, "Critical error. Consecutive failures: {FailureCount}", 
                    _consecutiveFailures);
                
                // Exponential backoff for repeated failures
                var delayMs = Math.Min(300000, 5000 * Math.Pow(2, Math.Min(_consecutiveFailures - 1, 6)));
                await Task.Delay(TimeSpan.FromMilliseconds(delayMs), stoppingToken);
            }
            
            await Task.Delay(5000, stoppingToken);
        }
    }
    
    public bool IsHealthy => DateTime.UtcNow - _lastSuccessfulRun < TimeSpan.FromMinutes(10);
}

public interface IPaymentService
{
    Task<List<Payment>> GetPendingPaymentsAsync(CancellationToken cancellationToken = default);
    Task ProcessPaymentAsync(int paymentId, CancellationToken cancellationToken = default);
}

public class Payment
{
    public int Id { get; set; }
}
