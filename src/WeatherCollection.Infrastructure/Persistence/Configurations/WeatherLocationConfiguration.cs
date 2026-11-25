using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeatherCollection.Domain.Entities;

namespace WeatherCollection.Infrastructure.Persistence.Configurations;

public class WeatherLocationConfiguration
    : IEntityTypeConfiguration<WeatherLocation>
{
    public void Configure(EntityTypeBuilder<WeatherLocation> builder)
    {
        builder.ToTable("WeatherLocations");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.Latitude).IsRequired();
        builder.Property(x => x.Longitude).IsRequired();

        builder.Property(x => x.LastError)
            .HasMaxLength(2000);

        builder.Property(x => x.ClaimedBy)
            .HasMaxLength(200);

        builder.HasIndex(x => new
        {
            x.Status,
            x.NextAttemptAtUtc,
            x.LeaseUntilUtc
        });

        builder.HasIndex(x => new
        {
            x.WeatherRequestId,
            x.Sequence
        }).IsUnique();

        builder.HasOne(x => x.Result)
        .WithOne()
        .HasForeignKey<WeatherResult>(x => x.WeatherLocationId)
        .OnDelete(DeleteBehavior.Cascade);
    }
}