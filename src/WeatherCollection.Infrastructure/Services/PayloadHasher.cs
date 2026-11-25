
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using WeatherCollection.Application.Abstractions.Services;

namespace WeatherCollection.Infrastructure.Services;

public sealed class PayloadHasher : IPayloadHasher
{
    public string ComputeHash(
        IEnumerable<(double Latitude, double Longitude)> locations)
    {
        var canonicalPayload = string.Join(
            "|",
            locations.Select(location =>
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{location.Latitude:R},{location.Longitude:R}")));

        var bytes = Encoding.UTF8.GetBytes(canonicalPayload);

        var hash = SHA256.HashData(bytes);

        return Convert.ToHexString(hash);
    }
}
