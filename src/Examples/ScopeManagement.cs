using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using BackgroundServicePatterns.Shared;

namespace BackgroundServicePatterns.Examples;

/// <summary>
/// Demonstrates proper service scope management to prevent memory leaks.
/// Shows the captive dependency problem and how to solve it.
/// </summary>
public class ScopeManagementExample : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScopeManagementExample> _logger;

    public ScopeManagementExample(
        IServiceScopeFactory scopeFactory,
        ILogger<ScopeManagementExample> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Scope management example started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessWithProperScopeManagement(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in processing loop");
                await Task.Delay(30000, stoppingToken);
            }

            await Task.Delay(5000, stoppingToken);
        }
    }

    /// <summary>
    /// ✅ CORRECT: Proper scope management prevents memory leaks
    /// Creates new scope for each batch, individual scopes for concurrent operations
    /// </summary>
    private async Task ProcessWithProperScopeManagement(CancellationToken stoppingToken)
    {
        // Create scope for the batch operation
        using var batchScope = _scopeFactory.CreateScope();
        var paymentService = batchScope.ServiceProvider.GetRequiredService<IPaymentService>();

        var payments = await paymentService.GetPendingPaymentsAsync(stoppingToken);
        _logger.LogInformation("Processing {Count} payments with proper scope management", payments.Count);

        // For concurrent operations, create individual scopes
        var tasks = payments.Select(async payment =>
        {
            // Each concurrent operation gets its own scope
            using var paymentScope = _scopeFactory.CreateScope();
            var scopedPaymentService = paymentScope.ServiceProvider.GetRequiredService<IPaymentService>();
            var scopedDbContext = paymentScope.ServiceProvider.GetRequiredService<IPaymentDbContext>();

            try
            {
                await scopedPaymentService.ProcessPaymentAsync(payment.Id, stoppingToken);

                // Scoped services are properly disposed when scope ends
                await scopedDbContext.SaveChangesAsync(stoppingToken);

                _logger.LogDebug("Processed payment {PaymentId} with individual scope", payment.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process payment {PaymentId}", payment.Id);
                throw;
            }
            // Scope automatically disposed here - prevents memory leaks
        });

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// ❌ WRONG: This creates captive dependencies and memory leaks
    /// Don't inject scoped services directly into singleton BackgroundService
    /// </summary>
    private Task AntipatternCaptiveDependency(CancellationToken stoppingToken)
    {
        // BAD: If you injected IPaymentService directly into constructor,
        // it would be captured for the lifetime of the singleton BackgroundService
        // causing memory leaks as the DbContext never gets disposed

        // This is what NOT to do:
        // private readonly IPaymentService _paymentService; // DON'T DO THIS
        // private readonly IPaymentDbContext _dbContext;   // DON'T DO THIS

        _logger.LogWarning("This method demonstrates what NOT to do - captive dependencies");
        return Task.CompletedTask;
    }

    /// <summary>
    /// ✅ ALTERNATIVE: For simple non-concurrent scenarios
    /// Single scope per batch is sufficient if no concurrent operations
    /// </summary>
    private async Task SimpleScopeManagement(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<IPaymentDbContext>();

        var payments = await paymentService.GetPendingPaymentsAsync(stoppingToken);

        // Process sequentially - no concurrency means we can reuse the same scope
        foreach (var payment in payments)
        {
            await paymentService.ProcessPaymentAsync(payment.Id, stoppingToken);
            _logger.LogDebug("Processed payment {PaymentId}", payment.Id);
        }

        await dbContext.SaveChangesAsync(stoppingToken);
    }
}

/// <summary>
/// Extension for demonstrating proper BackgroundService registration
/// </summary>
public static class ScopeManagementExtensions
{
    public static IServiceCollection AddScopeAwareBackgroundService<TService>(
        this IServiceCollection services)
        where TService : class, IHostedService
    {
        // Register as singleton - this is correct for BackgroundService
        services.AddSingleton<TService>();

        // Register hosted service
        services.AddHostedService(provider => provider.GetRequiredService<TService>());

        return services;
    }
}