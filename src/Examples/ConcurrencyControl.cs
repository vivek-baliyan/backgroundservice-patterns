using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace BackgroundServicePatterns.Examples;

/// <summary>
/// Demonstrates concurrency control to prevent resource exhaustion.
/// Shows both SemaphoreSlim and Channel-based approaches.
/// </summary>
public class ConcurrencyControlWithSemaphore : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ConcurrencyControlWithSemaphore> _logger;
    private readonly SemaphoreSlim _concurrencyLimiter;
    private const int MaxConcurrentOperations = 10;

    public ConcurrencyControlWithSemaphore(
        IServiceScopeFactory scopeFactory,
        ILogger<ConcurrencyControlWithSemaphore> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _concurrencyLimiter = new SemaphoreSlim(MaxConcurrentOperations, MaxConcurrentOperations);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();
                
                var pendingPayments = await paymentService.GetPendingPaymentsAsync(stoppingToken);
                
                // Process with concurrency control
                var tasks = pendingPayments.Select(async payment =>
                {
                    await _concurrencyLimiter.WaitAsync(stoppingToken);
                    try
                    {
                        using var innerScope = _scopeFactory.CreateScope();
                        var innerService = innerScope.ServiceProvider.GetRequiredService<IPaymentService>();
                        await innerService.ProcessPaymentAsync(payment.Id, stoppingToken);
                        
                        _logger.LogDebug("Processed payment {PaymentId}", payment.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to process payment {PaymentId}", payment.Id);
                    }
                    finally
                    {
                        _concurrencyLimiter.Release();
                    }
                });
                
                await Task.WhenAll(tasks);
                
                _logger.LogInformation("Processed batch of {Count} payments", pendingPayments.Count);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in payment processing loop");
                await Task.Delay(30000, stoppingToken);
            }
            
            await Task.Delay(5000, stoppingToken);
        }
    }

    public override void Dispose()
    {
        _concurrencyLimiter?.Dispose();
        base.Dispose();
    }
}

/// <summary>
/// Channel-based approach for high-throughput scenarios with natural backpressure.
/// Better for sustained high-volume processing.
/// </summary>
public class ConcurrencyControlWithChannels : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ConcurrencyControlWithChannels> _logger;
    private readonly Channel<PaymentRequest> _channel;
    private readonly ChannelWriter<PaymentRequest> _writer;
    private readonly ChannelReader<PaymentRequest> _reader;

    public ConcurrencyControlWithChannels(
        IServiceScopeFactory scopeFactory,
        ILogger<ConcurrencyControlWithChannels> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        
        var options = new BoundedChannelOptions(capacity: 1000)
        {
            FullMode = BoundedChannelFullMode.Wait, // Backpressure when full
            SingleReader = false,
            SingleWriter = false
        };
        
        _channel = Channel.CreateBounded<PaymentRequest>(options);
        _writer = _channel.Writer;
        _reader = _channel.Reader;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Start multiple consumer tasks (limited concurrency)
        var consumerTasks = Enumerable.Range(0, Environment.ProcessorCount)
            .Select(_ => ConsumePayments(stoppingToken))
            .ToArray();

        // Start producer task
        var producerTask = ProducePayments(stoppingToken);

        await Task.WhenAll(consumerTasks.Concat(new[] { producerTask }));
    }

    private async Task ProducePayments(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();
                
                var pendingPayments = await paymentService.GetPendingPaymentsAsync(stoppingToken);
                
                foreach (var payment in pendingPayments)
                {
                    await _writer.WriteAsync(new PaymentRequest(payment.Id), stoppingToken);
                }
                
                _logger.LogDebug("Queued {Count} payments for processing", pendingPayments.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error producing payments");
            }
            
            await Task.Delay(5000, stoppingToken);
        }
    }

    private async Task ConsumePayments(CancellationToken stoppingToken)
    {
        await foreach (var paymentRequest in _reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();
                
                await paymentService.ProcessPaymentAsync(paymentRequest.PaymentId, stoppingToken);
                _logger.LogDebug("Processed payment {PaymentId}", paymentRequest.PaymentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process payment {PaymentId}", paymentRequest.PaymentId);
            }
        }
    }

    public record PaymentRequest(int PaymentId);
}
