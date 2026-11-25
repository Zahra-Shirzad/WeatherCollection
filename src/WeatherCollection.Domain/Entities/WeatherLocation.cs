
using WeatherCollection.Domain.Enums;

namespace WeatherCollection.Domain.Entities;

public class WeatherLocation
{
    public Guid Id { get; private set; }

    public Guid WeatherRequestId { get; private set; }

    public double Latitude { get; private set; }

    public double Longitude { get; private set; }

    public int Sequence { get; private set; }

    public WeatherLocationStatus Status { get; private set; }

    public int AttemptCount { get; private set; }

    public DateTime? NextAttemptAtUtc { get; private set; }

    public DateTime? LeaseUntilUtc { get; private set; }

    public string? ClaimedBy { get; private set; }

    public long ClaimVersion { get; private set; }

    public string? LastError { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public WeatherResult? Result { get; private set; }

    private WeatherLocation()
    {
        // Required by EF Core
    }

    private WeatherLocation(
        Guid weatherRequestId,
        double latitude,
        double longitude,
        int sequence)
    {
        if (weatherRequestId == Guid.Empty)
            throw new ArgumentException(
                "Request ID cannot be empty.");

        if (latitude is < -90 or > 90)
            throw new ArgumentOutOfRangeException(
                nameof(latitude));

        if (longitude is < -180 or > 180)
            throw new ArgumentOutOfRangeException(
                nameof(longitude));

        if (sequence < 1)
            throw new ArgumentOutOfRangeException(
                nameof(sequence));

        Id = Guid.NewGuid();
        WeatherRequestId = weatherRequestId;
        Latitude = latitude;
        Longitude = longitude;
        Sequence = sequence;
        Status = WeatherLocationStatus.Pending;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static WeatherLocation Create(
        Guid weatherRequestId,
        double latitude,
        double longitude,
        int sequence)
    {
        return new WeatherLocation(
            weatherRequestId,
            latitude,
            longitude,
            sequence);
    }

    public void Claim(string workerId, DateTime leaseUntilUtc)
    {
        if (Status != WeatherLocationStatus.Pending &&
            !(Status == WeatherLocationStatus.Processing &&
            LeaseUntilUtc <= DateTime.UtcNow))
        {
            throw new InvalidOperationException(
                "Location is not available for claiming.");
        }

        if (string.IsNullOrWhiteSpace(workerId))
            throw new ArgumentException("Worker ID is required.");

        if (leaseUntilUtc <= DateTime.UtcNow)
            throw new ArgumentException(
                "Lease expiration must be in the future.");

        Status = WeatherLocationStatus.Processing;
        AttemptCount++;
        ClaimVersion++;
        ClaimedBy = workerId;
        LeaseUntilUtc = leaseUntilUtc;
        NextAttemptAtUtc = null;
    }

    public void RenewLease(
        long claimVersion,
        DateTime newLeaseUntilUtc)
    {
        EnsureCurrentClaim(claimVersion);

        if (newLeaseUntilUtc <= DateTime.UtcNow)
            throw new ArgumentException(
                "Lease expiration must be in the future.");

        LeaseUntilUtc = newLeaseUntilUtc;
    }

    public void MarkSucceeded(long claimVersion)
    {
        EnsureCurrentClaim(claimVersion);

        Status = WeatherLocationStatus.Succeeded;
        ClearLease();
        LastError = null;
    }

    public void ScheduleRetry(
        long claimVersion,
        DateTime nextAttemptAtUtc,
        string error)
    {
        EnsureCurrentClaim(claimVersion);

        if (nextAttemptAtUtc <= DateTime.UtcNow)
            throw new ArgumentException(
                "Next attempt must be in the future.");

        Status = WeatherLocationStatus.Pending;
        NextAttemptAtUtc = nextAttemptAtUtc;
        LastError = error;
        ClearLease();
    }

    public void ReleaseWithoutConsumingAttempt(
        long claimVersion,
        DateTime nextAttemptAtUtc,
        string? error)
    {
        EnsureCurrentClaim(claimVersion);

        if (nextAttemptAtUtc <= DateTime.UtcNow)
            throw new ArgumentException(
                "Next attempt must be in the future.");

        if (AttemptCount > 0)
            AttemptCount--;

        Status = WeatherLocationStatus.Pending;
        NextAttemptAtUtc = nextAttemptAtUtc;
        LastError = error;
        ClearLease();
    }

    public void MarkFailed(
        long claimVersion,
        string error)
    {
        EnsureCurrentClaim(claimVersion);

        Status = WeatherLocationStatus.Failed;
        LastError = error;
        ClearLease();
    }

    private void EnsureCurrentClaim(long claimVersion)
    {
        if (Status != WeatherLocationStatus.Processing ||
            ClaimVersion != claimVersion)
        {
            throw new InvalidOperationException(
                "Worker no longer owns this location.");
        }
    }

    private void ClearLease()
    {
        ClaimedBy = null;
        LeaseUntilUtc = null;
    }
}
