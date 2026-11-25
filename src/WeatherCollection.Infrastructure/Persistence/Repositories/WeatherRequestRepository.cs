using Microsoft.EntityFrameworkCore;
using WeatherCollection.Application.Abstractions.Persistence;
using WeatherCollection.Application.Exceptions;
using WeatherCollection.Domain.Entities;

namespace WeatherCollection.Infrastructure.Persistence.Repositories;

public sealed class WeatherRequestRepository
    : IWeatherRequestRepository
{
    private readonly WeatherCollectionDbContext _context;

    public WeatherRequestRepository(WeatherCollectionDbContext context)
    {
        _context = context;
    }

    public Task<WeatherRequest?> GetByRequestIdAsync(
        string requestId,
        CancellationToken cancellationToken)
    {
       return _context.WeatherRequests
            .Include(x => x.Locations)
                .ThenInclude(x => x.Result)
            .FirstOrDefaultAsync(
                x => x.RequestId == requestId,
                cancellationToken);
    }

    public async Task AddAsync(
        WeatherRequest request,
        CancellationToken cancellationToken)
    {
        await _context.WeatherRequests.AddAsync(
            request,
            cancellationToken);
    }

 public async Task SaveChangesAsync(
    CancellationToken cancellationToken)
{
    try
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
    catch (DbUpdateException exception)
        when (IsUniqueConstraintViolation(exception))
    {
        foreach (var entry in _context.ChangeTracker.Entries()
                     .Where(x => x.State == EntityState.Added)
                     .ToList())
        {
            entry.State = EntityState.Detached;
        }

        throw new DuplicateRequestIdException("Duplicate RequestId.");
    }
}

    private static bool IsUniqueConstraintViolation(
        DbUpdateException exception)
    {
        return exception.InnerException is Microsoft.Data.SqlClient.SqlException
        {
            Number: 2601 or 2627
        };
    }
}