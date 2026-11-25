using WeatherCollection.Domain.Entities;

namespace WeatherCollection.Application.Abstractions.Persistence;

public interface IWeatherWorkRepository
{
    Task<IReadOnlyList<WeatherLocation>> ClaimNextBatchAsync(
    string workerId,
    int batchSize,
    int maxAttempts,
    TimeSpan leaseDuration,
    CancellationToken cancellationToken);

    Task<bool> RenewLeaseAsync(
        Guid locationId,
        long claimVersion,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken);

    Task<bool> MarkSucceededAsync(
        Guid locationId,
        long claimVersion,
        WeatherResult result,
        CancellationToken cancellationToken);

    Task<bool> ScheduleRetryAsync(
        Guid locationId,
        long claimVersion,
        DateTime nextAttemptAtUtc,
        string error,
        CancellationToken cancellationToken);

    Task<bool> ReleaseWithoutConsumingAttemptAsync(
        Guid locationId,
        long claimVersion,
        DateTime nextAttemptAtUtc,
        string? error,
        CancellationToken cancellationToken);

    Task<bool> MarkFailedAsync(
        Guid locationId,
        long claimVersion,
        string error,
        CancellationToken cancellationToken);
}