using WeatherCollection.Application.Abstractions.Persistence;
using WeatherCollection.Application.DTOs.Responses;
using WeatherCollection.Domain.Enums;

namespace WeatherCollection.Application.Features.WeatherRequests.GetResults;

public sealed class GetWeatherRequestResultsHandler
{
    private readonly IWeatherRequestRepository _repository;

    public GetWeatherRequestResultsHandler(
        IWeatherRequestRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<WeatherLocationResultResponse>?> HandleAsync(
        string requestId,
        CancellationToken cancellationToken)
    {
        var request = await _repository.GetByRequestIdAsync(
            requestId,
            cancellationToken);

        if (request is null)
            return null;

        return request.Locations
            .OrderBy(x => x.Sequence)
            .Select(location =>
            {
                var result = location.Result;

                return new WeatherLocationResultResponse(
                    location.Latitude,
                    location.Longitude,
                    location.Status.ToString(),
                    result?.ReceivedAtUtc,
                    result?.Temperature,
                    result?.RelativeHumidity,
                    result?.WindSpeed,
                    location.LastError
                );
            })
            .ToList();
    }
}