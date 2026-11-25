namespace WeatherCollection.Application.Exceptions;

public sealed class DuplicateRequestIdException : Exception
{
    public DuplicateRequestIdException(string requestId)
        : base($"Request ID '{requestId}' already exists.")
    {
    }
}