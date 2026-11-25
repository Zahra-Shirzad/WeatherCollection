namespace WeatherCollection.Application.Exceptions;

public sealed class RequestIdConflictException : Exception
{
    public RequestIdConflictException(string requestId)
        : base($"Request ID '{requestId}' already exists with a different payload.")
    {
    }
}