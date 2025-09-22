using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using BackgroundServicePatterns.Shared;

namespace BackgroundServicePatterns.Examples;

/// <summary>
/// Demonstrates proper cancellation token handling for graceful shutdown.
/// Critical for Kubernetes deployments and cloud environments.
/// </summary>
public class GracefulShutdownExample : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GracefulShutdownExample> _logger;
    private readonly List<Task> _activeTasks = new();
    private readonly object _tasksLock = new();

    public GracefulShutdownExample(
        IServiceScopeFactory scopeFactory,
        ILogger<GracefulShutdownExample> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Service started with graceful shutdown support");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();

                var payments = await paymentService.GetPendingPaymentsAsync(stoppingToken);

                // Start concurrent tasks but track them for graceful shutdown
                var processingTasks = payments.Select(payment =>
                    ProcessPaymentWithTracking(payment, stoppingToken)).ToArray();

                await Task.WhenAll(processingTasks);

                _logger.LogDebug("Completed processing batch of {Count} payments", payments.Count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Shutdown requested, stopping gracefully");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in processing loop");

                // Respect cancellation even during error recovery
                try
                {
                    await Task.Delay(30000, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Shutdown requested during error recovery");
                    break;
                }
            }

            // Respect cancellation token in delays
            try
            {
                await Task.Delay(5000, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Shutdown requested during normal delay");
                break;
            }
        }

        // Wait for all active tasks to complete during shutdown
        await WaitForActiveTasksToComplete();
        _logger.LogInformation("Service stopped gracefully");
    }

    private async Task ProcessPaymentWithTracking(Payment payment, CancellationToken stoppingToken)
    {
        var taskCompletionSource = new TaskCompletionSource();
        var processingTask = taskCompletionSource.Task;

        // Track active tasks for graceful shutdown
        lock (_tasksLock)
        {
            _activeTasks.Add(processingTask);
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();

            // Check cancellation before starting work
            stoppingToken.ThrowIfCancellationRequested();

            await paymentService.ProcessPaymentAsync(payment.Id, stoppingToken);
            _logger.LogDebug("Processed payment {PaymentId}", payment.Id);

            taskCompletionSource.SetResult();
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogDebug("Payment {PaymentId} processing cancelled", payment.Id);
            taskCompletionSource.SetCanceled();
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process payment {PaymentId}", payment.Id);
            taskCompletionSource.SetException(ex);
            throw;
        }
        finally
        {
            // Remove from active tasks when complete
            lock (_tasksLock)
            {
                _activeTasks.Remove(processingTask);
            }
        }
    }

    private async Task WaitForActiveTasksToComplete()
    {
        List<Task> tasksToWait;
        lock (_tasksLock)
        {
            tasksToWait = new List<Task>(_activeTasks);
        }

        if (tasksToWait.Any())
        {
            _logger.LogInformation("Waiting for {Count} active tasks to complete", tasksToWait.Count);

            try
            {
                // Give active tasks up to 30 seconds to complete - using latest .NET 8+ pattern
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await Task.WhenAll(tasksToWait).WaitAsync(timeoutCts.Token);
                _logger.LogInformation("All active tasks completed gracefully");
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("Some tasks did not complete within timeout period");
            }
        }
    }
}