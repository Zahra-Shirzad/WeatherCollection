namespace WeatherCollection.Infrastructure.Options;

public sealed class WeatherWorkerOptions
{
    public const string SectionName = "WeatherWorker";

    public int MaxConcurrency { get; set; } = 5;

    public int BatchSize { get; set; } = 10;

    public int PollingIntervalSeconds { get; set; } = 5;

    public int MaxAttempts { get; set; } = 5;

    public int LeaseDurationSeconds { get; set; } = 60;

    public int DependencyBackoffSeconds { get; set; } = 30;
}