using WeatherCollection.Application.Abstractions.Persistence;
using WeatherCollection.Application.DTOs.Requests;
using WeatherCollection.Application.Exceptions;
using WeatherCollection.Application.Features.WeatherRequests.Create;
using WeatherCollection.Domain.Entities;
using WeatherCollection.Infrastructure.Services;

namespace WeatherCollection.Tests.Application;

public sealed class CreateWeatherRequestHandlerTests
{
    private readonly InMemoryWeatherRequestRepository _repository = new();
    private readonly CreateWeatherRequestHandler _handler;

    public CreateWeatherRequestHandlerTests()
    {
        _handler = new CreateWeatherRequestHandler(
            _repository,
            new PayloadHasher());
    }

    [Fact]
    public async Task Rejects_an_empty_location_list()
    {
        var dto = new CreateWeatherRequestDto("REQ-1", []);

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _handler.HandleAsync(dto, CancellationToken.None));

        Assert.Contains("location", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(91, 10)]
    [InlineData(10, 181)]
    public async Task Rejects_coordinates_outside_the_valid_range(double latitude, double longitude)
    {
        var dto = new CreateWeatherRequestDto(
            "REQ-1",
            [new WeatherLocationDto(latitude, longitude)]);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _handler.HandleAsync(dto, CancellationToken.None));
    }

    [Fact]
    public async Task Same_payload_returns_the_existing_request_without_creating_another()
    {
        var dto = Request("REQ-1", 35.6892, 51.3890);
        await _handler.HandleAsync(dto, CancellationToken.None);

        var duplicate = await _handler.HandleAsync(dto, CancellationToken.None);

        Assert.True(duplicate.IsDuplicate);
        Assert.Equal("REQ-1", duplicate.RequestId);
        Assert.Equal(1, _repository.Count);
    }

    [Fact]
    public async Task Different_payload_for_the_same_id_is_rejected()
    {
        await _handler.HandleAsync(
            Request("REQ-1", 35.6892, 51.3890),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<RequestIdConflictException>(
            () => _handler.HandleAsync(
                Request("REQ-1", 48.8566, 2.3522),
                CancellationToken.None));

        Assert.Contains("REQ-1", exception.Message);
    }

    [Fact]
    public async Task Concurrent_insert_with_the_same_payload_returns_the_winner()
    {
        var existing = WeatherRequest.Create(
            "REQ-1",
            new PayloadHasher().ComputeHash([(35.6892, 51.3890)]),
            [(35.6892, 51.3890)]);
        _repository.Seed(existing);
        _repository.ThrowOnSave = true;

        var response = await _handler.HandleAsync(
            Request("REQ-1", 35.6892, 51.3890),
            CancellationToken.None);

        Assert.True(response.IsDuplicate);
        Assert.Equal(existing.RequestId, response.RequestId);
    }

    private static CreateWeatherRequestDto Request(
        string requestId,
        double latitude,
        double longitude)
    {
        return new CreateWeatherRequestDto(
            requestId,
            [new WeatherLocationDto(latitude, longitude)]);
    }

    private sealed class InMemoryWeatherRequestRepository : IWeatherRequestRepository
    {
        private readonly Dictionary<string, WeatherRequest> _items = new(StringComparer.Ordinal);
        private WeatherRequest? _pending;
        private int _reads;

        public bool ThrowOnSave { get; set; }

        public int Count => _items.Count;

        public void Seed(WeatherRequest request)
        {
            _items[request.RequestId] = request;
        }

        public Task<WeatherRequest?> GetByRequestIdAsync(
            string requestId,
            CancellationToken cancellationToken)
        {
            _reads++;

            if (_reads == 1)
                return Task.FromResult<WeatherRequest?>(null);

            _items.TryGetValue(requestId, out var request);
            return Task.FromResult(request);
        }

        public Task AddAsync(WeatherRequest request, CancellationToken cancellationToken)
        {
            _pending = request;
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            if (ThrowOnSave)
                throw new DuplicateRequestIdException("Duplicate RequestId.");

            if (_pending is not null)
                _items[_pending.RequestId] = _pending;

            return Task.CompletedTask;
        }
    }
}
