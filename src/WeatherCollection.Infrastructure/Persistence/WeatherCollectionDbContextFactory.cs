using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WeatherCollection.Infrastructure.Persistence;

public sealed class WeatherCollectionDbContextFactory
    : IDesignTimeDbContextFactory<WeatherCollectionDbContext>
{
    public WeatherCollectionDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            "Server=localhost;Database=WeatherCollectionDb;Trusted_Connection=True;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<WeatherCollectionDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new WeatherCollectionDbContext(options);
    }
}
