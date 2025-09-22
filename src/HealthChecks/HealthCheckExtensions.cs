using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using BackgroundServicePatterns.Templates;

namespace BackgroundServicePatterns.HealthChecks;

/// <summary>
/// Extension methods for registering BackgroundService health checks.
/// Integrates with ASP.NET Core health check middleware for monitoring dashboards.
/// </summary>
public static class HealthCheckExtensions
{
    /// <summary>
    /// Adds a health check for any BackgroundService that exposes health properties.
    /// Uses reflection to detect IsHealthy, LastSuccessfulRun, and ConsecutiveFailures properties.
    /// </summary>
    public static IServiceCollection AddBackgroundServiceHealthCheck<TService>(
        this IServiceCollection services,
        string name,
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null,
        TimeSpan? timeout = null)
        where TService : class
    {
        return services.AddHealthChecks()
            .AddCheck<BackgroundServiceHealthCheck<TService>>(
                name,
                failureStatus,
                tags,
                timeout)
            .Services;
    }

    /// <summary>
    /// Adds a health check specifically for the ProductionBackgroundServiceTemplate.
    /// Provides detailed health status with metrics.
    /// </summary>
    public static IServiceCollection AddProductionBackgroundServiceHealthCheck(
        this IServiceCollection services,
        string name = "background-service",
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null,
        TimeSpan? timeout = null)
    {
        return services.AddHealthChecks()
            .AddCheck<ProductionBackgroundServiceHealthCheck>(
                name,
                failureStatus,
                tags ?? new[] { "background-service", "ready" },
                timeout)
            .Services;
    }

    /// <summary>
    /// Adds multiple BackgroundService health checks at once.
    /// Useful for applications with multiple background services.
    /// Note: This is a simplified version. For complex scenarios, register each health check individually.
    /// </summary>
    public static IServiceCollection AddBackgroundServicesHealthChecks(
        this IServiceCollection services,
        params (Type serviceType, string name)[] serviceConfigs)
    {
        // For simplicity in this patterns repository, we'll add a basic implementation
        // In production, you would typically register each health check individually
        // using the AddBackgroundServiceHealthCheck<T> method above

        services.AddHealthChecks()
            .AddCheck("background-services", () => HealthCheckResult.Healthy("Background services registered"),
                tags: new[] { "background-service" });

        return services;
    }

    /// <summary>
    /// Adds comprehensive health checks for a BackgroundService with custom thresholds.
    /// Allows fine-tuning of health check sensitivity.
    /// </summary>
    public static IServiceCollection AddBackgroundServiceHealthCheckWithThresholds<TService>(
        this IServiceCollection services,
        string name,
        TimeSpan? maxTimeSinceLastSuccess = null,
        int? maxConsecutiveFailures = null,
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null)
        where TService : class
    {
        services.AddSingleton<CustomThresholdHealthCheck<TService>>(provider =>
        {
            var service = provider.GetRequiredService<TService>();
            var logger = provider.GetRequiredService<ILogger<CustomThresholdHealthCheck<TService>>>();

            return new CustomThresholdHealthCheck<TService>(
                service,
                logger,
                maxTimeSinceLastSuccess ?? TimeSpan.FromMinutes(10),
                maxConsecutiveFailures ?? 5);
        });

        return services.AddHealthChecks()
            .AddCheck<CustomThresholdHealthCheck<TService>>(
                name,
                failureStatus,
                tags,
                null)
            .Services;
    }

    /// <summary>
    /// Configures health check endpoints for Kubernetes probes.
    /// Sets up both readiness and liveness endpoints.
    /// </summary>
    public static IServiceCollection AddKubernetesHealthChecks(this IServiceCollection services)
    {
        return services.AddHealthChecks()
            .AddCheck("ready", () => HealthCheckResult.Healthy(), tags: new[] { "ready" })
            .AddCheck("live", () => HealthCheckResult.Healthy(), tags: new[] { "live" })
            .Services;
    }
}

/// <summary>
/// Health check with custom thresholds for more granular control.
/// </summary>
public class CustomThresholdHealthCheck<TService> : IHealthCheck
    where TService : class
{
    private readonly TService _service;
    private readonly ILogger<CustomThresholdHealthCheck<TService>> _logger;
    private readonly TimeSpan _maxTimeSinceLastSuccess;
    private readonly int _maxConsecutiveFailures;

    public CustomThresholdHealthCheck(
        TService service,
        ILogger<CustomThresholdHealthCheck<TService>> logger,
        TimeSpan maxTimeSinceLastSuccess,
        int maxConsecutiveFailures)
    {
        _service = service;
        _logger = logger;
        _maxTimeSinceLastSuccess = maxTimeSinceLastSuccess;
        _maxConsecutiveFailures = maxConsecutiveFailures;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var serviceType = typeof(TService);
            var lastSuccessfulRunProperty = serviceType.GetProperty("LastSuccessfulRun");
            var consecutiveFailuresProperty = serviceType.GetProperty("ConsecutiveFailures");

            var data = new Dictionary<string, object>
            {
                ["service_type"] = serviceType.Name,
                ["max_time_since_success_minutes"] = _maxTimeSinceLastSuccess.TotalMinutes,
                ["max_consecutive_failures"] = _maxConsecutiveFailures
            };

            if (lastSuccessfulRunProperty?.GetValue(_service) is DateTime lastRun)
            {
                var timeSinceLastRun = DateTime.UtcNow - lastRun;
                data["last_successful_run"] = lastRun;
                data["minutes_since_last_success"] = timeSinceLastRun.TotalMinutes;

                if (timeSinceLastRun > _maxTimeSinceLastSuccess)
                {
                    var message = $"{serviceType.Name} exceeded max time since success: {timeSinceLastRun.TotalMinutes:F1} > {_maxTimeSinceLastSuccess.TotalMinutes} minutes";
                    return Task.FromResult(HealthCheckResult.Unhealthy(message, data: data));
                }
            }

            if (consecutiveFailuresProperty?.GetValue(_service) is long failures)
            {
                data["consecutive_failures"] = failures;

                if (failures > _maxConsecutiveFailures)
                {
                    var message = $"{serviceType.Name} exceeded max consecutive failures: {failures} > {_maxConsecutiveFailures}";
                    return Task.FromResult(HealthCheckResult.Unhealthy(message, data: data));
                }
            }

            return Task.FromResult(HealthCheckResult.Healthy($"{serviceType.Name} within all thresholds", data));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Custom threshold health check failed for {ServiceType}", typeof(TService).Name);
            return Task.FromResult(HealthCheckResult.Unhealthy($"Unable to verify {typeof(TService).Name} status", ex));
        }
    }
}