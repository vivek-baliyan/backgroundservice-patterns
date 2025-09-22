using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using BackgroundServicePatterns.Shared;

namespace BackgroundServicePatterns.Examples;

/// <summary>
/// Demonstrates comprehensive monitoring and observability patterns.
/// Includes metrics, structured logging, and performance tracking.
/// </summary>
public class MonitoringExample : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MonitoringExample> _logger;
    private readonly ActivitySource _activitySource;
    private readonly Meter _meter;

    // Metrics
    private readonly Counter<long> _paymentsProcessedCounter;
    private readonly Counter<long> _paymentsFailedCounter;
    private readonly Histogram<double> _processingDurationHistogram;
    private readonly UpDownCounter<long> _activePaymentsGauge;
    private readonly ObservableGauge<long> _consecutiveFailuresGauge;

    // Health tracking
    private DateTime _lastSuccessfulRun = DateTime.UtcNow;
    private long _consecutiveFailures = 0;
    private long _totalProcessed = 0;
    private long _totalFailed = 0;
    private long _activeOperations = 0;

    public MonitoringExample(
        IServiceScopeFactory scopeFactory,
        ILogger<MonitoringExample> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        // OpenTelemetry tracing - latest naming conventions
        _activitySource = new ActivitySource("BackgroundServicePatterns.PaymentProcessor", "1.0.0");

        // OpenTelemetry metrics - using semantic conventions
        _meter = new Meter("BackgroundServicePatterns.PaymentProcessor", "1.0.0");

        // Latest OpenTelemetry semantic conventions for metrics
        _paymentsProcessedCounter = _meter.CreateCounter<long>(
            "backgroundservice.payments.processed",
            unit: "{payment}",
            description: "Total number of payments successfully processed");

        _paymentsFailedCounter = _meter.CreateCounter<long>(
            "backgroundservice.payments.failed",
            unit: "{payment}",
            description: "Total number of payment processing failures");

        _processingDurationHistogram = _meter.CreateHistogram<double>(
            "backgroundservice.payment.processing.duration",
            unit: "s",
            description: "Duration of payment processing operations");

        _activePaymentsGauge = _meter.CreateUpDownCounter<long>(
            "backgroundservice.payments.active",
            unit: "{payment}",
            description: "Number of payments currently being processed");

        _consecutiveFailuresGauge = _meter.CreateObservableGauge<long>(
            "backgroundservice.failures.consecutive",
            unit: "{failure}",
            observeValue: () => _consecutiveFailures,
            description: "Current number of consecutive processing failures");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var activity = _activitySource.StartActivity("PaymentProcessor.Execute");
        activity?.SetTag("service.name", "payment-processor");

        _logger.LogInformation("Payment processor started with monitoring");

        while (!stoppingToken.IsCancellationRequested)
        {
            var batchStartTime = DateTime.UtcNow;

            try
            {
                using var batchActivity = _activitySource.StartActivity("PaymentProcessor.ProcessBatch");

                await ProcessBatchWithMonitoring(stoppingToken);

                _lastSuccessfulRun = DateTime.UtcNow;
                _consecutiveFailures = 0;

                var batchDuration = DateTime.UtcNow - batchStartTime;

                // Structured logging with context
                _logger.LogInformation("Batch processing completed successfully. " +
                    "Duration: {Duration}ms, TotalProcessed: {TotalProcessed}, TotalFailed: {TotalFailed}",
                    batchDuration.TotalMilliseconds, _totalProcessed, _totalFailed);

                batchActivity?.SetTag("batch.success", true);
                batchActivity?.SetTag("batch.duration_ms", batchDuration.TotalMilliseconds);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Shutdown requested");
                break;
            }
            catch (Exception ex)
            {
                _consecutiveFailures++;

                using var errorActivity = _activitySource.StartActivity("PaymentProcessor.Error");
                errorActivity?.SetTag("error.type", ex.GetType().Name);
                errorActivity?.SetTag("consecutive_failures", _consecutiveFailures);

                _logger.LogError(ex, "Batch processing failed. " +
                    "ConsecutiveFailures: {ConsecutiveFailures}, LastSuccess: {LastSuccess}",
                    _consecutiveFailures, _lastSuccessfulRun);

                var delayMs = Math.Min(300000, 5000 * Math.Pow(2, Math.Min(_consecutiveFailures - 1, 6)));
                await Task.Delay(TimeSpan.FromMilliseconds(delayMs), stoppingToken);
            }

            await Task.Delay(5000, stoppingToken);
        }
    }

    private async Task ProcessBatchWithMonitoring(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();

        var payments = await paymentService.GetPendingPaymentsAsync(stoppingToken);

        if (!payments.Any())
        {
            _logger.LogDebug("No pending payments found");
            return;
        }

        _logger.LogInformation("Processing batch of {PaymentCount} payments", payments.Count);

        var tasks = payments.Select(payment => ProcessPaymentWithMetrics(payment, stoppingToken));
        await Task.WhenAll(tasks);
    }

    private async Task ProcessPaymentWithMetrics(Payment payment, CancellationToken stoppingToken)
    {
        using var activity = _activitySource.StartActivity("PaymentProcessor.ProcessPayment");
        activity?.SetTag("payment.id", payment.Id);

        var stopwatch = Stopwatch.StartNew();

        // Track active operations
        _activePaymentsGauge.Add(1);
        Interlocked.Increment(ref _activeOperations);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();

            await paymentService.ProcessPaymentAsync(payment.Id, stoppingToken);

            stopwatch.Stop();

            // Record successful processing metrics
            _paymentsProcessedCounter.Add(1, new KeyValuePair<string, object?>("payment.type", "standard"));
            _processingDurationHistogram.Record(stopwatch.Elapsed.TotalSeconds);

            Interlocked.Increment(ref _totalProcessed);

            activity?.SetTag("payment.status", "success");
            activity?.SetTag("processing.duration_ms", stopwatch.ElapsedMilliseconds);

            _logger.LogDebug("Payment {PaymentId} processed successfully in {Duration}ms",
                payment.Id, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // Record failure metrics
            _paymentsFailedCounter.Add(1,
                new KeyValuePair<string, object?>("error.type", ex.GetType().Name),
                new KeyValuePair<string, object?>("payment.id", payment.Id));

            Interlocked.Increment(ref _totalFailed);

            activity?.SetTag("payment.status", "failed");
            activity?.SetTag("error.type", ex.GetType().Name);
            activity?.SetTag("error.message", ex.Message);

            _logger.LogError(ex, "Payment {PaymentId} processing failed after {Duration}ms",
                payment.Id, stopwatch.ElapsedMilliseconds);

            throw;
        }
        finally
        {
            // Track active operations
            _activePaymentsGauge.Add(-1);
            Interlocked.Decrement(ref _activeOperations);
        }
    }

    // Health check properties
    public DateTime LastSuccessfulRun => _lastSuccessfulRun;
    public long ConsecutiveFailures => _consecutiveFailures;
    public long TotalProcessed => _totalProcessed;
    public long TotalFailed => _totalFailed;
    public long ActiveOperations => _activeOperations;
    public bool IsHealthy => DateTime.UtcNow - _lastSuccessfulRun < TimeSpan.FromMinutes(10) && _consecutiveFailures < 5;

    public override void Dispose()
    {
        _activitySource?.Dispose();
        _meter?.Dispose();
        base.Dispose();
    }
}

/// <summary>
/// Extension methods for monitoring setup
/// </summary>
public static class MonitoringExtensions
{
    public static IServiceCollection AddMonitoredBackgroundService<TService>(
        this IServiceCollection services,
        string serviceName = "background-service")
        where TService : class, IHostedService
    {
        services.AddSingleton<TService>();
        services.AddHostedService(provider => provider.GetRequiredService<TService>());

        // Add OpenTelemetry configuration here if needed
        return services;
    }
}