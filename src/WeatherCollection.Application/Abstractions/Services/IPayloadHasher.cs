namespace WeatherCollection.Application.Abstractions.Services;

public interface IPayloadHasher
{
    string ComputeHash(
        IEnumerable<(double Latitude, double Longitude)> locations);
}