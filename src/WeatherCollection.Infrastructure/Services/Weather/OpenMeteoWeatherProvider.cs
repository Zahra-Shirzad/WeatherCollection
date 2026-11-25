using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Polly.CircuitBreaker;
using Polly.Timeout;
using WeatherCollection.Application.Abstractions.Services;
using WeatherCollection.Application.Exceptions;

namespace WeatherCollection.Infrastructure.Services.Weather;

public sealed class OpenMeteoWeatherProvider : IWeatherProvider
{
    private readonly HttpClient _httpClient;

    public OpenMeteoWeatherProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<WeatherData> GetCurrentWeatherAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken)
    {
        var lat = latitude.ToString(CultureInfo.InvariantCulture);
        var lon = longitude.ToString(CultureInfo.InvariantCulture);

        var url =
            $"v1/forecast?latitude={lat}&longitude={lon}" +
            "&current=temperature_2m,relative_humidity_2m,wind_speed_10m" +
            "&temperature_unit=celsius&wind_speed_unit=kmh&timezone=GMT";

        try
        {
            using var response = await _httpClient.GetAsync(
                url,
                cancellationToken);

            if (IsTransient(response.StatusCode))
            {
                throw new WeatherProviderException(
                    $"Open-Meteo returned {(int)response.StatusCode}.",
                    isTransient: true);
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new WeatherProviderException(
                    $"Open-Meteo rejected the request with {(int)response.StatusCode}.",
                    isTransient: false);
            }

            var data = await response.Content.ReadFromJsonAsync<OpenMeteoResponse>(
                cancellationToken);

            return Map(data);
        }
        catch (WeatherProviderException)
        {
            throw;
        }
        catch (BrokenCircuitException exception)
        {
            throw new WeatherProviderException(
                "Open-Meteo is temporarily unavailable.",
                isTransient: true,
                isDependencyUnavailable: true,
                exception);
        }
        catch (TimeoutRejectedException exception)
        {
            throw new WeatherProviderException(
                "Open-Meteo timed out.",
                isTransient: true,
                innerException: exception);
        }
        catch (HttpRequestException exception)
        {
            throw new WeatherProviderException(
                "Open-Meteo could not be reached.",
                isTransient: true,
                innerException: exception);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException exception)
        {
            throw new WeatherProviderException(
                "Open-Meteo timed out.",
                isTransient: true,
                innerException: exception);
        }
        catch (Exception exception)
        {
            throw new WeatherProviderException(
                "Open-Meteo returned a response that cannot be used.",
                isTransient: false,
                innerException: exception);
        }
    }

    private static bool IsTransient(HttpStatusCode statusCode)
    {
        var code = (int)statusCode;
        return statusCode == HttpStatusCode.RequestTimeout
            || statusCode == HttpStatusCode.TooManyRequests
            || code >= 500;
    }

    private static WeatherData Map(OpenMeteoResponse? data)
    {
        if (data?.Current is null)
        {
            throw new WeatherProviderException(
                "Open-Meteo returned an empty current weather response.",
                isTransient: false);
        }

        if (data.Current.Temperature is null ||
            data.Current.RelativeHumidity is null ||
            data.Current.WindSpeed is null ||
            string.IsNullOrWhiteSpace(data.Current.Time))
        {
            throw new WeatherProviderException(
                "Open-Meteo returned incomplete weather data.",
                isTransient: false);
        }

        if (!DateTime.TryParse(
                data.Current.Time,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var weatherTimeUtc))
        {
            throw new WeatherProviderException(
                "Open-Meteo returned an invalid weather timestamp.",
                isTransient: false);
        }

        return new WeatherData(
            data.Current.Temperature.Value,
            data.Current.RelativeHumidity.Value,
            data.Current.WindSpeed.Value,
            DateTime.SpecifyKind(weatherTimeUtc, DateTimeKind.Utc));
    }

    private sealed class OpenMeteoResponse
    {
        [JsonPropertyName("current")]
        public CurrentWeather? Current { get; set; }
    }

    private sealed class CurrentWeather
    {
        [JsonPropertyName("temperature_2m")]
        public double? Temperature { get; set; }

        [JsonPropertyName("relative_humidity_2m")]
        public double? RelativeHumidity { get; set; }

        [JsonPropertyName("wind_speed_10m")]
        public double? WindSpeed { get; set; }

        [JsonPropertyName("time")]
        public string? Time { get; set; }
    }
}
