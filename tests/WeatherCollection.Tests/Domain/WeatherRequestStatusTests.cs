using WeatherCollection.Domain.Entities;
using WeatherCollection.Domain.Enums;

namespace WeatherCollection.Tests.Domain;

public sealed class WeatherRequestStatusTests
{
    [Fact]
    public void New_request_stays_pending_before_any_attempt()
    {
        var request = CreateRequest();

        request.RecalculateStatus();

        Assert.Equal(WeatherRequestStatus.Pending, request.Status);
        Assert.Null(request.StartedAtUtc);
        Assert.Null(request.CompletedAtUtc);
    }

    [Fact]
    public void All_succeeded_locations_complete_the_request()
    {
        var (request, locations) = CreateWithLocations();

        foreach (var location in locations)
            Succeed(location);

        request.RecalculateStatus();

        Assert.Equal(WeatherRequestStatus.Completed, request.Status);
        Assert.NotNull(request.StartedAtUtc);
        Assert.NotNull(request.CompletedAtUtc);
    }

    [Fact]
    public void All_failed_locations_fail_the_request()
    {
        var (request, locations) = CreateWithLocations();

        foreach (var location in locations)
            Fail(location);

        request.RecalculateStatus();

        Assert.Equal(WeatherRequestStatus.Failed, request.Status);
        Assert.NotNull(request.CompletedAtUtc);
    }

    [Fact]
    public void Mixed_terminal_locations_are_partially_completed()
    {
        var (request, locations) = CreateWithLocations();

        Succeed(locations[0]);
        Fail(locations[1]);

        request.RecalculateStatus();

        Assert.Equal(WeatherRequestStatus.PartiallyCompleted, request.Status);
    }

    [Fact]
    public void Pending_retry_keeps_the_request_processing()
    {
        var (request, locations) = CreateWithLocations();

        Fail(locations[0]);
        var version = Claim(locations[1]);
        locations[1].ScheduleRetry(
            version,
            DateTime.UtcNow.AddMinutes(1),
            "temporary");

        request.RecalculateStatus();

        Assert.Equal(WeatherRequestStatus.Processing, request.Status);
        Assert.Null(request.CompletedAtUtc);
        Assert.NotNull(request.StartedAtUtc);
    }

    [Fact]
    public void Releasing_a_claim_without_a_real_attempt_does_not_consume_the_budget()
    {
        var location = CreateWithLocations().Locations[0];
        var version = Claim(location);

        location.ReleaseWithoutConsumingAttempt(
            version,
            DateTime.UtcNow.AddSeconds(30),
            "circuit open");

        Assert.Equal(0, location.AttemptCount);
        Assert.Equal(WeatherLocationStatus.Pending, location.Status);
        Assert.Null(location.LeaseUntilUtc);
        Assert.Equal("circuit open", location.LastError);
    }

    private static WeatherRequest CreateRequest()
    {
        return WeatherRequest.Create(
            "REQ-STATUS",
            "hash",
            [(35.6892, 51.3890), (48.8566, 2.3522)]);
    }

    private static (WeatherRequest Request, WeatherLocation[] Locations) CreateWithLocations()
    {
        var request = CreateRequest();
        var locations = request.Locations.OrderBy(x => x.Sequence).ToArray();
        return (request, locations);
    }

    private static long Claim(WeatherLocation location)
    {
        location.Claim("worker", DateTime.UtcNow.AddMinutes(1));
        return location.ClaimVersion;
    }

    private static void Succeed(WeatherLocation location)
    {
        var version = Claim(location);
        location.MarkSucceeded(version);
    }

    private static void Fail(WeatherLocation location)
    {
        var version = Claim(location);
        location.MarkFailed(version, "failed");
    }
}
