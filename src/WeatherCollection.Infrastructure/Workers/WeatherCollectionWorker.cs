using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WeatherCollection.Application.Abstractions.Persistence;
using WeatherCollection.Application.Abstractions.Services;
using WeatherCollection.Application.Exceptions;
using WeatherCollection.Application.Features.WeatherRequests;
using WeatherCollection.Domain.Entities;
using WeatherCollection.Infrastructure.Options;

namespace WeatherCollection.Infrastructure.Workers;

public sealed class WeatherCollectionWorker : BackgroundService
{
    private readonly ILogger<WeatherCollectionWorker> _logger;
    private readonly WeatherWorkerOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;

    private readonly string _workerId =
        $"{Environment.MachineName}:{Guid.NewGuid():N}";

    public WeatherCollectionWorker(
        ILogger<WeatherCollectionWorker> logger,
        IOptions<WeatherWorkerOptions> options,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _options = options.Value;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation("Weather Collection Worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var dependencyUnavailable = 0;

            try
            {
                using var scope = _scopeFactory.CreateScope();

                var repository = scope.ServiceProvider
                    .GetRequiredService<IWeatherWorkRepository>();

                var locations = await repository.ClaimNextBatchAsync(
                    _workerId,
                    _options.BatchSize,
                    _options.MaxAttempts,
                    TimeSpan.FromSeconds(_options.LeaseDurationSeconds),
                    stoppingToken);

                if (locations.Count == 0)
                {
                    _logger.LogDebug("No weather locations are ready to claim.");
                }
                else
                {
                    _logger.LogInformation(
                        "Claimed {Count} weather locations.",
                        locations.Count);
                }

                await Parallel.ForEachAsync(
                    locations,
                    new ParallelOptions
                    {
                        MaxDegreeOfParallelism = Math.Max(1, _options.MaxConcurrency),
                        CancellationToken = stoppingToken
                    },
                    async (location, token) =>
                    {
                        var outcome = await ProcessLocationAsync(location, token);

                        if (outcome == ProcessOutcome.DependencyUnavailable)
                            Interlocked.Increment(ref dependencyUnavailable);
                    });
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "An error occurred while claiming weather locations.");
            }

            var delaySeconds = dependencyUnavailable > 0
                ? Math.Max(_options.PollingIntervalSeconds, _options.DependencyBackoffSeconds)
                : _options.PollingIntervalSeconds;

            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(Math.Max(1, delaySeconds)),
                    stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task<ProcessOutcome> ProcessLocationAsync(
        WeatherLocation location,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        var repository = scope.ServiceProvider
            .GetRequiredService<IWeatherWorkRepository>();

        var provider = scope.ServiceProvider
            .GetRequiredService<IWeatherProvider>();

        try
        {
            _logger.LogInformation(
                "Processing location {LocationId} ({Latitude}, {Longitude})",
                location.Id,
                location.Latitude,
                location.Longitude);

            var weather = await provider.GetCurrentWeatherAsync(
                location.Latitude,
                location.Longitude,
                cancellationToken);

            var result = WeatherResult.Create(
                location.Id,
                weather.Temperature,
                weather.RelativeHumidity,
                weather.WindSpeed,
                weather.WeatherTimeUtc);

            var saved = await repository.MarkSucceededAsync(
                location.Id,
                location.ClaimVersion,
                result,
                cancellationToken);

            if (saved)
            {
                _logger.LogInformation(
                    "Location {LocationId} processed successfully.",
                    location.Id);
            }

            return ProcessOutcome.Succeeded;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (WeatherProviderException exception)
        {
            _logger.LogWarning(
                exception,
                "Weather provider failed for location {LocationId}.",
                location.Id);

            return await ApplyFailureAsync(
                repository,
                location,
                exception.IsDependencyUnavailable,
                exception.IsTransient,
                exception.Message,
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to process location {LocationId}.",
                location.Id);

            return await ApplyFailureAsync(
                repository,
                location,
                isDependencyUnavailable: false,
                isTransient: true,
                exception.Message,
                cancellationToken);
        }
    }

    private async Task<ProcessOutcome> ApplyFailureAsync(
        IWeatherWorkRepository repository,
        WeatherLocation location,
        bool isDependencyUnavailable,
        bool isTransient,
        string error,
        CancellationToken cancellationToken)
    {
        var truncated = error.Length > 2000 ? error[..2000] : error;

        var action = WeatherFailurePolicy.Decide(
            isDependencyUnavailable,
            isTransient,
            location.AttemptCount,
            _options.MaxAttempts);

        switch (action)
        {
            case WeatherFailureAction.ReleaseWithoutConsumingAttempt:
                var unavailableDelay = TimeSpan.FromSeconds(
                    Math.Max(1, _options.DependencyBackoffSeconds) + Random.Shared.Next(0, 3));

                await repository.ReleaseWithoutConsumingAttemptAsync(
                    location.Id,
                    location.ClaimVersion,
                    DateTime.UtcNow.Add(unavailableDelay),
                    truncated,
                    cancellationToken);

                return ProcessOutcome.DependencyUnavailable;

            case WeatherFailureAction.Fail:
                await repository.MarkFailedAsync(
                    location.Id,
                    location.ClaimVersion,
                    truncated,
                    cancellationToken);

                return ProcessOutcome.Failed;

            default:
                var retryDelay = WeatherFailurePolicy.ComputeRetryDelay(location.AttemptCount);

                await repository.ScheduleRetryAsync(
                    location.Id,
                    location.ClaimVersion,
                    DateTime.UtcNow.Add(retryDelay),
                    truncated,
                    cancellationToken);

                return ProcessOutcome.RetryScheduled;
        }
    }

    private enum ProcessOutcome
    {
        Succeeded,
        Failed,
        RetryScheduled,
        DependencyUnavailable
    }
}
