using Microsoft.EntityFrameworkCore;
using WeatherCollection.Domain.Entities;

namespace WeatherCollection.Infrastructure.Persistence;

public class WeatherCollectionDbContext : DbContext
{
    public WeatherCollectionDbContext(
        DbContextOptions<WeatherCollectionDbContext> options)
        : base(options)
    {
    }

    public DbSet<WeatherRequest> WeatherRequests => Set<WeatherRequest>();

    public DbSet<WeatherLocation> WeatherLocations => Set<WeatherLocation>();

    public DbSet<WeatherResult> WeatherResults => Set<WeatherResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(WeatherCollectionDbContext).Assembly);
    }
}