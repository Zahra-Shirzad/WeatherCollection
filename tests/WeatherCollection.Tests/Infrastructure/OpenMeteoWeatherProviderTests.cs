using System.Net;
using System.Text;
using Polly.CircuitBreaker;
using Polly.Timeout;
using WeatherCollection.Application.Exceptions;
using WeatherCollection.Infrastructure.Services.Weather;

namespace WeatherCollection.Tests.Infrastructure;

public sealed class OpenMeteoWeatherProviderTests
{
    [Fact]
    public async Task Reads_the_current_weather_fields()
    {
        var provider = Create(_ => JsonResponse(HttpStatusCode.OK, """
            {
              "current": {
                "temperature_2m": 21.5,
                "relative_humidity_2m": 40,
                "wind_speed_10m": 12.3,
                "time": "2026-11-25T08:00"
              }
            }
            """));

        var weather = await provider.GetCurrentWeatherAsync(35.6892, 51.3890, CancellationToken.None);

        Assert.Equal(21.5, weather.Temperature);
        Assert.Equal(40, weather.RelativeHumidity);
        Assert.Equal(12.3, weather.WindSpeed);
        Assert.Equal(DateTimeKind.Utc, weather.WeatherTimeUtc.Kind);
    }

    [Fact]
    public async Task Client_error_is_permanent_and_is_not_retried()
    {
        var handler = new StubHandler(_ => JsonResponse(HttpStatusCode.BadRequest, "{}"));
        var provider = new OpenMeteoWeatherProvider(Client(handler));

        var exception = await Assert.ThrowsAsync<WeatherProviderException>(
            () => provider.GetCurrentWeatherAsync(1, 2, CancellationToken.None));

        Assert.False(exception.IsTransient);
        Assert.False(exception.IsDependencyUnavailable);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task Server_and_rate_limit_errors_are_transient(HttpStatusCode statusCode)
    {
        var provider = Create(_ => JsonResponse(statusCode, "{}"));

        var exception = await Assert.ThrowsAsync<WeatherProviderException>(
            () => provider.GetCurrentWeatherAsync(1, 2, CancellationToken.None));

        Assert.True(exception.IsTransient);
        Assert.False(exception.IsDependencyUnavailable);
    }

    [Fact]
    public async Task Timeout_is_transient()
    {
        var provider = Create((_, _) => throw new TimeoutRejectedException("timed out"));

        var exception = await Assert.ThrowsAsync<WeatherProviderException>(
            () => provider.GetCurrentWeatherAsync(1, 2, CancellationToken.None));

        Assert.True(exception.IsTransient);
        Assert.False(exception.IsDependencyUnavailable);
    }

    [Fact]
    public async Task Open_circuit_does_not_count_as_a_failed_attempt_signal()
    {
        var provider = Create((_, _) => throw new BrokenCircuitException("open"));

        var exception = await Assert.ThrowsAsync<WeatherProviderException>(
            () => provider.GetCurrentWeatherAsync(1, 2, CancellationToken.None));

        Assert.True(exception.IsDependencyUnavailable);
        Assert.True(exception.IsTransient);
    }

    [Fact]
    public async Task Incomplete_success_payload_is_permanent()
    {
        var provider = Create(_ => JsonResponse(HttpStatusCode.OK, """{"current":{}}"""));

        var exception = await Assert.ThrowsAsync<WeatherProviderException>(
            () => provider.GetCurrentWeatherAsync(1, 2, CancellationToken.None));

        Assert.False(exception.IsTransient);
    }

    private static OpenMeteoWeatherProvider Create(
        Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        return Create((_, _) => Task.FromResult(responder(null!)));
    }

    private static OpenMeteoWeatherProvider Create(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
    {
        return new OpenMeteoWeatherProvider(Client(new StubHandler(responder)));
    }

    private static HttpClient Client(HttpMessageHandler handler)
    {
        return new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.open-meteo.com/")
        };
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _send;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> send)
            : this((_, _) => Task.FromResult(send(null!)))
        {
        }

        public StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        {
            _send = send;
        }

        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Calls++;
            return _send(request, cancellationToken);
        }
    }
}
