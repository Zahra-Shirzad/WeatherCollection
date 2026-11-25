using Microsoft.EntityFrameworkCore;
using WeatherCollection.Application.Features.WeatherRequests.Create;
using WeatherCollection.Application.Features.WeatherRequests.GetById;
using WeatherCollection.Application.Features.WeatherRequests.GetResults;
using WeatherCollection.Infrastructure;
using WeatherCollection.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddScoped<GetWeatherRequestStatusHandler>();
builder.Services.AddScoped<GetWeatherRequestResultsHandler>();
builder.Services.AddScoped<CreateWeatherRequestHandler>();

var app = builder.Build();

await ApplyMigrationsAsync(app);

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.Run();

static async Task ApplyMigrationsAsync(WebApplication app)
{
    const int maxAttempts = 10;

    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            await using var scope = app.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<WeatherCollectionDbContext>();
            await db.Database.MigrateAsync();
            return;
        }
        catch (Exception exception) when (attempt < maxAttempts)
        {
            app.Logger.LogWarning(
                exception,
                "Database migration attempt {Attempt} failed. Retrying.",
                attempt);

            await Task.Delay(TimeSpan.FromSeconds(3));
        }
    }
}

public partial class Program;
