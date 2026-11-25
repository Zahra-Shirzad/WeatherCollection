using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using WeatherCollection.Application.Abstractions.Persistence;
using WeatherCollection.Application.Abstractions.Services;
using WeatherCollection.Infrastructure.Options;
using WeatherCollection.Infrastructure.Persistence;
using WeatherCollection.Infrastructure.Persistence.Repositories;
using WeatherCollection.Infrastructure.Services;
using WeatherCollection.Infrastructure.Services.Weather;
using WeatherCollection.Infrastructure.Workers;

namespace WeatherCollection.Infrastructure;

public static class DependencyInjection
{
    public const int ProviderTimeoutSeconds = 20;

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(
            "DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "DefaultConnection connection string is missing.");

        services.AddDbContext<WeatherCollectionDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IWeatherRequestRepository, WeatherRequestRepository>();
        services.AddScoped<IWeatherWorkRepository, WeatherWorkRepository>();
        services.AddSingleton<IPayloadHasher, PayloadHasher>();
        services.Configure<WeatherWorkerOptions>(
            configuration.GetSection(WeatherWorkerOptions.SectionName));

        services.AddHttpClient<IWeatherProvider, OpenMeteoWeatherProvider>(
                client =>
                {
                    client.BaseAddress = new Uri("https://api.open-meteo.com/");
                    client.Timeout = Timeout.InfiniteTimeSpan;
                })
            .AddResilienceHandler("open-meteo", builder =>
            {
                builder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                {
                    FailureRatio = 0.5,
                    MinimumThroughput = 5,
                    SamplingDuration = TimeSpan.FromSeconds(30),
                    BreakDuration = TimeSpan.FromSeconds(30)
                });

                builder.AddTimeout(TimeSpan.FromSeconds(ProviderTimeoutSeconds));
            });

        services.AddHostedService<WeatherCollectionWorker>();

        return services;
    }
}
