
using WeatherCollection.Domain.Enums;

namespace WeatherCollection.Domain.Entities;

public class WeatherRequest
{
    public Guid Id { get; private set; }

    public string RequestId { get; private set; } = null!;

    public string PayloadHash { get; private set; } = null!;

    public WeatherRequestStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? StartedAtUtc { get; private set; }

    public DateTime? CompletedAtUtc { get; private set; }

    private readonly List<WeatherLocation> _locations = [];

    public IReadOnlyCollection<WeatherLocation> Locations =>
        _locations.AsReadOnly();

    private WeatherRequest()
    {
        // Required by EF Core
    }

    private WeatherRequest(
        string requestId,
        string payloadHash,
        IEnumerable<(double Latitude, double Longitude)> locations)
    {
        if (string.IsNullOrWhiteSpace(requestId))
            throw new ArgumentException(
                "Request ID is required.");

        if (string.IsNullOrWhiteSpace(payloadHash))
            throw new ArgumentException(
                "Payload hash is required.");

        var coordinates = locations?.ToList()
            ?? throw new ArgumentNullException(nameof(locations));

        if (coordinates.Count == 0)
            throw new ArgumentException(
                "At least one location is required.");

        Id = Guid.NewGuid();
        RequestId = requestId.Trim();
        PayloadHash = payloadHash;
        Status = WeatherRequestStatus.Pending;
        CreatedAtUtc = DateTime.UtcNow;

        for (var i = 0; i < coordinates.Count; i++)
        {
            var coordinate = coordinates[i];

            _locations.Add(
                WeatherLocation.Create(
                    Id,
                    coordinate.Latitude,
                    coordinate.Longitude,
                    i + 1));
        }
    }

    public static WeatherRequest Create(
        string requestId,
        string payloadHash,
        IEnumerable<(double Latitude, double Longitude)> locations)
    {
        return new WeatherRequest(
            requestId,
            payloadHash,
            locations);
    }

    public void MarkStarted()
    {
        StartedAtUtc ??= DateTime.UtcNow;
        Status = WeatherRequestStatus.Processing;
    }

    
    public void RecalculateStatus()
    {
        if (_locations.Count == 0)
            throw new InvalidOperationException("Request has no locations.");

        var total = _locations.Count;

        var succeeded = _locations.Count(
            x => x.Status == WeatherLocationStatus.Succeeded);

        var failed = _locations.Count(
            x => x.Status == WeatherLocationStatus.Failed);

        var processing = _locations.Any(
            x => x.Status == WeatherLocationStatus.Processing);

        var attempted = _locations.Any(x => x.AttemptCount > 0);

        var completed = succeeded + failed;

        if (completed == total)
        {
            Status = succeeded == total
                ? WeatherRequestStatus.Completed
                : succeeded == 0
                    ? WeatherRequestStatus.Failed
                    : WeatherRequestStatus.PartiallyCompleted;

            StartedAtUtc ??= DateTime.UtcNow;
            CompletedAtUtc ??= DateTime.UtcNow;
            return;
        }

        CompletedAtUtc = null;

        if (processing || attempted || completed > 0)
        {
            StartedAtUtc ??= DateTime.UtcNow;
            Status = WeatherRequestStatus.Processing;
            return;
        }

        Status = WeatherRequestStatus.Pending;
    }

}
