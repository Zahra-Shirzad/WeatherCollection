using WeatherCollection.Domain.Entities;

namespace WeatherCollection.Application.Abstractions.Persistence;

public interface IWeatherRequestRepository
{
    Task<WeatherRequest?> GetByRequestIdAsync(
        string requestId,
        CancellationToken cancellationToken);

    Task AddAsync(
        WeatherRequest request,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(
        CancellationToken cancellationToken);
}