namespace WeatherCollection.Application.DTOs.Responses;

public sealed record WeatherLocationResultResponse(
    double Latitude,
    double Longitude,
    string Status,
    DateTime? ReceivedAtUtc,
    double? Temperature,
    double? RelativeHumidity,
    double? WindSpeed,
    string? Error
);