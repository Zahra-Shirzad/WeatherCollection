namespace WeatherCollection.Domain.Enums;

public enum WeatherRequestStatus
{
    Pending = 1,
    Processing = 2,
    Completed = 3,
    PartiallyCompleted = 4,
    Failed = 5
}