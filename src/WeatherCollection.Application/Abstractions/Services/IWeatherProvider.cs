namespace WeatherCollection.Application.Abstractions.Services;

public interface IWeatherProvider
{
    Task<WeatherData> GetCurrentWeatherAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken);
}

public sealed record WeatherData(
    double Temperature,
    double RelativeHumidity,
    double WindSpeed,
    DateTime WeatherTimeUtc
);