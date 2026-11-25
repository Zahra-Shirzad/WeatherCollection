using WeatherCollection.Application.Abstractions.Persistence;
using WeatherCollection.Application.Abstractions.Services;
using WeatherCollection.Application.DTOs.Requests;
using WeatherCollection.Application.DTOs.Responses;
using WeatherCollection.Application.Exceptions;
using WeatherCollection.Domain.Entities;

namespace WeatherCollection.Application.Features.WeatherRequests.Create;

public sealed class CreateWeatherRequestHandler
{
    private readonly IWeatherRequestRepository _repository;
    private readonly IPayloadHasher _payloadHasher;

    public CreateWeatherRequestHandler(
        IWeatherRequestRepository repository,
        IPayloadHasher payloadHasher)
    {
        _repository = repository;
        _payloadHasher = payloadHasher;
    }

    public async Task<CreateWeatherRequestResponse> HandleAsync(
        CreateWeatherRequestDto dto,
        CancellationToken cancellationToken)
    {
        Validate(dto);

        var requestId = dto.RequestId.Trim();

        var locations = dto.Locations
            .Select(x => (x.Latitude, x.Longitude))
            .ToList();

        var payloadHash = _payloadHasher.ComputeHash(locations);

        var existingRequest = await _repository.GetByRequestIdAsync(
            requestId,
            cancellationToken);

        if (existingRequest is not null)
        {
            return CreateDuplicateResponse(
                existingRequest,
                payloadHash);
        }

        var request = WeatherRequest.Create(
            requestId,
            payloadHash,
            locations);

        await _repository.AddAsync(request, cancellationToken);

        try
        {
            await _repository.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateRequestIdException)
        {
            for (var attempt = 0; attempt < 5; attempt++)
            {
                var concurrentRequest = await _repository.GetByRequestIdAsync(
                    requestId,
                    cancellationToken);

                if (concurrentRequest is not null)
                    return CreateDuplicateResponse(concurrentRequest, payloadHash);

                await Task.Delay(TimeSpan.FromMilliseconds(20), cancellationToken);
            }

            throw;
        }

        return new CreateWeatherRequestResponse(
            request.RequestId,
            request.Status.ToString(),
            false);
    }

    private static CreateWeatherRequestResponse CreateDuplicateResponse(
        WeatherRequest existingRequest,
        string payloadHash)
    {
        if (!string.Equals(
                existingRequest.PayloadHash,
                payloadHash,
                StringComparison.Ordinal))
        {
            throw new RequestIdConflictException(
                existingRequest.RequestId);
        }

        return new CreateWeatherRequestResponse(
            existingRequest.RequestId,
            existingRequest.Status.ToString(),
            true);
    }

    private static void Validate(CreateWeatherRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (string.IsNullOrWhiteSpace(dto.RequestId))
            throw new ArgumentException("Request ID is required.");

        if (dto.Locations is null || dto.Locations.Count == 0)
            throw new ArgumentException(
                "At least one location is required.");

        foreach (var location in dto.Locations)
        {
            if (location.Latitude is < -90 or > 90 ||
                !double.IsFinite(location.Latitude))
            {
                throw new ArgumentException(
                    "Latitude must be between -90 and 90.");
            }

            if (location.Longitude is < -180 or > 180 ||
                !double.IsFinite(location.Longitude))
            {
                throw new ArgumentException(
                    "Longitude must be between -180 and 180.");
            }
        }
    }
}