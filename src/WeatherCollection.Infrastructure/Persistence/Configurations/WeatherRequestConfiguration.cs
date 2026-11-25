using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeatherCollection.Domain.Entities;

namespace WeatherCollection.Infrastructure.Persistence.Configurations;

public class WeatherRequestConfiguration
    : IEntityTypeConfiguration<WeatherRequest>
{
    public void Configure(EntityTypeBuilder<WeatherRequest> builder)
    {
        builder.ToTable("WeatherRequests");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.RequestId)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(x => x.RequestId)
            .IsUnique();

        builder.Property(x => x.PayloadHash)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc).IsRequired();

        builder.HasMany(x => x.Locations)
            .WithOne()
            .HasForeignKey(x => x.WeatherRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Locations)
            .HasField("_locations")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}