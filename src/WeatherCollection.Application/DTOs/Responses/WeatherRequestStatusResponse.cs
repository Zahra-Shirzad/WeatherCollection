namespace WeatherCollection.Application.DTOs.Responses;

public sealed record WeatherRequestStatusResponse(
    string RequestId,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc,
    int TotalLocations,
    int ProcessedLocations,
    int SucceededLocations,
    int FailedLocations
);