using WeatherCollection.Infrastructure.Services;

namespace WeatherCollection.Tests.Infrastructure;

public sealed class PayloadHasherTests
{
    private readonly PayloadHasher _hasher = new();

    [Fact]
    public void Same_coordinates_in_the_same_order_produce_the_same_hash()
    {
        var first = _hasher.ComputeHash([(35.6892, 51.3890), (48.8566, 2.3522)]);
        var second = _hasher.ComputeHash([(35.6892, 51.3890), (48.8566, 2.3522)]);

        Assert.Equal(first, second);
    }

    [Fact]
    public void A_different_order_is_a_different_payload()
    {
        var original = _hasher.ComputeHash([(35.6892, 51.3890), (48.8566, 2.3522)]);
        var reordered = _hasher.ComputeHash([(48.8566, 2.3522), (35.6892, 51.3890)]);

        Assert.NotEqual(original, reordered);
    }
}
