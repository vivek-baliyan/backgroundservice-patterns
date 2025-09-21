using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using BackgroundServicePatterns.Templates;

namespace BackgroundServicePatterns.HealthChecks;

/// <summary>
/// Generic health check for BackgroundServices that expose health status.
/// Integrates with Kubernetes readiness probes and monitoring dashboards.
/// </summary>
public class BackgroundServiceHealthCheck<TService> : IHealthCheck
    where TService : class
{
    private readonly TService _service;
    private readonly ILogger<BackgroundServiceHealthCheck<TService>> _logger;

    public BackgroundServiceHealthCheck(TService service, ILogger<BackgroundServiceHealthCheck<TService>> logger)
    {
        _service = service;
        _logger = logger;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Use reflection to check if service has health properties
            var serviceType = typeof(TService);
            var isHealthyProperty = serviceType.GetProperty("IsHealthy");
            var lastSuccessfulRunProperty = serviceType.GetProperty("LastSuccessfulRun");
            var consecutiveFailuresProperty = serviceType.GetProperty("ConsecutiveFailures");

            if (isHealthyProperty?.GetValue(_service) is bool isHealthy)
            {
                var data = new Dictionary<string, object>
                {
                    ["service_type"] = serviceType.Name
                };

                if (lastSuccessfulRunProperty?.GetValue(_service) is DateTime lastRun)
                {
                    var timeSinceLastRun = DateTime.UtcNow - lastRun;
                    data["last_successful_run"] = lastRun;
                    data["minutes_since_last_success"] = timeSinceLastRun.TotalMinutes;
                }

                if (consecutiveFailuresProperty?.GetValue(_service) is long failures)
                {
                    data["consecutive_failures"] = failures;
                }

                if (!isHealthy)
                {
                    var message = $"{serviceType.Name} is unhealthy";
                    if (data.ContainsKey("minutes_since_last_success"))
                    {
                        message += $". Last success: {data["minutes_since_last_success"]:F1} minutes ago";
                    }
                    if (data.ContainsKey("consecutive_failures") && (long)data["consecutive_failures"] > 0)
                    {
                        message += $". Consecutive failures: {data["consecutive_failures"]}";
                    }

                    return Task.FromResult(HealthCheckResult.Unhealthy(message, data: data));
                }

                // Check if service is degraded (running but slow)
                if (data.ContainsKey("minutes_since_last_success") && (double)data["minutes_since_last_success"] > 5)
                {
                    var message = $"{serviceType.Name} hasn't completed successfully for {data["minutes_since_last_success"]:F1} minutes";
                    return Task.FromResult(HealthCheckResult.Degraded(message, data: data));
                }

                return Task.FromResult(HealthCheckResult.Healthy($"{serviceType.Name} running normally", data));
            }

            // Fallback if service doesn't implement health interface
            return Task.FromResult(HealthCheckResult.Healthy($"{serviceType.Name} is running"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed for {ServiceType}", typeof(TService).Name);
            return Task.FromResult(HealthCheckResult.Unhealthy($"Unable to verify {typeof(TService).Name} status", ex));
        }
    }
}

/// <summary>
/// Specific health check for the production template service.
/// </summary>
public class ProductionBackgroundServiceHealthCheck : IHealthCheck
{
    private readonly ProductionBackgroundServiceTemplate _service;
    private readonly ILogger<ProductionBackgroundServiceHealthCheck> _logger;

    public ProductionBackgroundServiceHealthCheck(
        ProductionBackgroundServiceTemplate service,
        ILogger<ProductionBackgroundServiceHealthCheck> logger)
    {
        _service = service;
        _logger = logger;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var timeSinceLastSuccess = DateTime.UtcNow - _service.LastSuccessfulRun;
            
            var data = new Dictionary<string, object>
            {
                ["last_successful_run"] = _service.LastSuccessfulRun,
                ["consecutive_failures"] = _service.ConsecutiveFailures,
                ["minutes_since_last_success"] = timeSinceLastSuccess.TotalMinutes,
                ["is_healthy"] = _service.IsHealthy
            };

            if (!_service.IsHealthy)
            {
                var message = $"Background service unhealthy. " +
                             $"Last success: {timeSinceLastSuccess.TotalMinutes:F1} min ago, " +
                             $"Consecutive failures: {_service.ConsecutiveFailures}";
                
                return Task.FromResult(HealthCheckResult.Unhealthy(message, data: data));
            }

            if (timeSinceLastSuccess > TimeSpan.FromMinutes(5))
            {
                var message = $"Background service hasn't completed successfully for {timeSinceLastSuccess.TotalMinutes:F1} minutes";
                return Task.FromResult(HealthCheckResult.Degraded(message, data: data));
            }

            return Task.FromResult(HealthCheckResult.Healthy("Background service running normally", data));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return Task.FromResult(HealthCheckResult.Unhealthy("Unable to verify background service status", ex));
        }
    }
}
