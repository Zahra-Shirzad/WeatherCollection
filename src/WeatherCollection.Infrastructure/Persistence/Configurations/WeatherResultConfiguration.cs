using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeatherCollection.Domain.Entities;

namespace WeatherCollection.Infrastructure.Persistence.Configurations;

public class WeatherResultConfiguration
    : IEntityTypeConfiguration<WeatherResult>
{
    public void Configure(EntityTypeBuilder<WeatherResult> builder)
    {
        builder.ToTable("WeatherResults");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.WeatherLocationId)
            .IsUnique();

        builder.Property(x => x.Temperature)
            .IsRequired();

        builder.Property(x => x.RelativeHumidity)
            .IsRequired();

        builder.Property(x => x.WindSpeed)
            .IsRequired();

        builder.Property(x => x.WeatherTimeUtc)
            .IsRequired();

        builder.Property(x => x.ReceivedAtUtc)
            .IsRequired();
    }
}