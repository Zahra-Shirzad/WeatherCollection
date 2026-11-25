using WeatherCollection.Application.Features.WeatherRequests;

namespace WeatherCollection.Tests.Application;

public sealed class WeatherFailurePolicyTests
{
    [Fact]
    public void Dependency_outage_does_not_consume_an_attempt()
    {
        var action = WeatherFailurePolicy.Decide(
            isDependencyUnavailable: true,
            isTransient: true,
            attemptCount: 5,
            maxAttempts: 5);

        Assert.Equal(WeatherFailureAction.ReleaseWithoutConsumingAttempt, action);
    }

    [Theory]
    [InlineData(1, 5)]
    [InlineData(4, 5)]
    public void Transient_failures_are_retried_inside_the_budget(int attemptCount, int maxAttempts)
    {
        var action = WeatherFailurePolicy.Decide(
            isDependencyUnavailable: false,
            isTransient: true,
            attemptCount,
            maxAttempts);

        Assert.Equal(WeatherFailureAction.Retry, action);
    }

    [Fact]
    public void Transient_failures_stop_at_the_attempt_limit()
    {
        var action = WeatherFailurePolicy.Decide(
            isDependencyUnavailable: false,
            isTransient: true,
            attemptCount: 5,
            maxAttempts: 5);

        Assert.Equal(WeatherFailureAction.Fail, action);
    }

    [Fact]
    public void Permanent_failures_are_not_retried()
    {
        var action = WeatherFailurePolicy.Decide(
            isDependencyUnavailable: false,
            isTransient: false,
            attemptCount: 1,
            maxAttempts: 5);

        Assert.Equal(WeatherFailureAction.Fail, action);
    }

    [Fact]
    public void Retry_delay_is_positive_and_bounded()
    {
        var delay = WeatherFailurePolicy.ComputeRetryDelay(3);

        Assert.InRange(delay, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(305));
    }
}
