namespace WeatherCollection.Application.Features.WeatherRequests;

public enum WeatherFailureAction
{
    Fail,
    Retry,
    ReleaseWithoutConsumingAttempt
}

public static class WeatherFailurePolicy
{
    public static WeatherFailureAction Decide(
        bool isDependencyUnavailable,
        bool isTransient,
        int attemptCount,
        int maxAttempts)
    {
        if (isDependencyUnavailable)
            return WeatherFailureAction.ReleaseWithoutConsumingAttempt;

        if (!isTransient || attemptCount >= maxAttempts)
            return WeatherFailureAction.Fail;

        return WeatherFailureAction.Retry;
    }

    public static TimeSpan ComputeRetryDelay(int attemptCount)
    {
        var exponent = Math.Clamp(attemptCount, 0, 8);
        var delaySeconds = Math.Min(300d, Math.Pow(2, exponent));
        var jitterSeconds = Random.Shared.Next(1, 6);

        return TimeSpan.FromSeconds(delaySeconds + jitterSeconds);
    }
}
