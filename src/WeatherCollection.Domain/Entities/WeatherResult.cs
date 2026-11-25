
namespace WeatherCollection.Domain.Entities;

public class WeatherResult
{
    public Guid Id { get; private set; }

    public Guid WeatherLocationId { get; private set; }

    public double Temperature { get; private set; }

    public double RelativeHumidity { get; private set; }

    public double WindSpeed { get; private set; }

    public DateTime WeatherTimeUtc { get; private set; }

    public DateTime ReceivedAtUtc { get; private set; }

    private WeatherResult()
    {
        // Required by EF Core
    }

    private WeatherResult(
        Guid weatherLocationId,
        double temperature,
        double relativeHumidity,
        double windSpeed,
        DateTime weatherTimeUtc)
    {
        if (weatherLocationId == Guid.Empty)
            throw new ArgumentException(
                "Weather location ID cannot be empty.");

        if (relativeHumidity is < 0 or > 100)
            throw new ArgumentOutOfRangeException(
                nameof(relativeHumidity));

        if (windSpeed < 0)
            throw new ArgumentOutOfRangeException(
                nameof(windSpeed));

        if (weatherTimeUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException(
                "Weather time must be UTC.");

        Id = Guid.NewGuid();
        WeatherLocationId = weatherLocationId;
        Temperature = temperature;
        RelativeHumidity = relativeHumidity;
        WindSpeed = windSpeed;
        WeatherTimeUtc = weatherTimeUtc;
        ReceivedAtUtc = DateTime.UtcNow;
    }

    public static WeatherResult Create(
        Guid weatherLocationId,
        double temperature,
        double relativeHumidity,
        double windSpeed,
        DateTime weatherTimeUtc)
    {
        if (!double.IsFinite(temperature) ||
            !double.IsFinite(relativeHumidity) ||
            !double.IsFinite(windSpeed))
        {
            throw new ArgumentException(
                "Weather values must be finite numbers.");
        }

        return new WeatherResult(
            weatherLocationId,
            temperature,
            relativeHumidity,
            windSpeed,
            weatherTimeUtc);
    }
}
