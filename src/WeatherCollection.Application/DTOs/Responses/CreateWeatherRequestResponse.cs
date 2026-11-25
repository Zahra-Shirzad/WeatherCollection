

namespace WeatherCollection.Application.DTOs.Responses;
public sealed record CreateWeatherRequestResponse(
    string RequestId,
    string Status,
    bool IsDuplicate);