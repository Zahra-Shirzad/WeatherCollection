using WeatherCollection.Application.Abstractions.Persistence;
using WeatherCollection.Application.DTOs.Responses;
using WeatherCollection.Domain.Enums;

namespace WeatherCollection.Application.Features.WeatherRequests.GetById;

public sealed class GetWeatherRequestStatusHandler
{
    private readonly IWeatherRequestRepository _repository;

    public GetWeatherRequestStatusHandler(
        IWeatherRequestRepository repository)
    {
        _repository = repository;
    }

    public async Task<WeatherRequestStatusResponse?> HandleAsync(
        string requestId,
        CancellationToken cancellationToken)
    {
        var request = await _repository.GetByRequestIdAsync(
            requestId,
            cancellationToken);

        if (request is null)
            return null;

        var locations = request.Locations.ToList();

        var succeeded = locations.Count(x =>
            x.Status == WeatherLocationStatus.Succeeded);

        var failed = locations.Count(x =>
            x.Status == WeatherLocationStatus.Failed);

        var processed = succeeded + failed;

        return new WeatherRequestStatusResponse(
            request.RequestId,
            request.Status.ToString(),
            request.CreatedAtUtc,
            request.StartedAtUtc,
            request.CompletedAtUtc,
            locations.Count,
            processed,
            succeeded,
            failed
        );
    }
}