

namespace WeatherCollection.Application.DTOs.Requests;

public sealed record CreateWeatherRequestDto(
    string RequestId,
    List<WeatherLocationDto> Locations);

public sealed record WeatherLocationDto(
    double Latitude,
    double Longitude);