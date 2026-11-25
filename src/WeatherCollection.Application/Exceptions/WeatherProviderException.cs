namespace WeatherCollection.Application.Exceptions;

public sealed class WeatherProviderException : Exception
{
    public WeatherProviderException(
        string message,
        bool isTransient,
        bool isDependencyUnavailable = false,
        Exception? innerException = null)
        : base(message, innerException)
    {
        IsTransient = isTransient;
        IsDependencyUnavailable = isDependencyUnavailable;
    }

    public bool IsTransient { get; }

    public bool IsDependencyUnavailable { get; }
}
