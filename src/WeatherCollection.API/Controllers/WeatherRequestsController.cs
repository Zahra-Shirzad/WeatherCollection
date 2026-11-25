
using Microsoft.AspNetCore.Mvc;
using WeatherCollection.Application.Features.WeatherRequests.Create;
using WeatherCollection.Application.Features.WeatherRequests.GetById;
using WeatherCollection.Application.Features.WeatherRequests.GetResults;
using WeatherCollection.Application.DTOs.Requests;
using WeatherCollection.Application.Exceptions;

namespace WeatherCollection.API.Controllers;

[ApiController]
[Route("api/weather-requests")]
public sealed class WeatherRequestsController : ControllerBase
{
    private readonly CreateWeatherRequestHandler _createHandler;
    private readonly GetWeatherRequestStatusHandler _statusHandler;
    private readonly GetWeatherRequestResultsHandler _resultsHandler;

    public WeatherRequestsController(
        CreateWeatherRequestHandler createHandler,
        GetWeatherRequestStatusHandler statusHandler,
        GetWeatherRequestResultsHandler resultsHandler)
    {
        _createHandler = createHandler;
        _statusHandler = statusHandler;
        _resultsHandler = resultsHandler;
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateWeatherRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _createHandler.HandleAsync(
                request,
                cancellationToken);

            return AcceptedAtAction(
                nameof(GetStatus),
                new { requestId = result.RequestId },
                result);
        }
        catch (RequestIdConflictException exception)
        {
            return Conflict(new
            {
                message = exception.Message
            });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new
            {
                message = exception.Message
            });
        }
    }

    [HttpGet("{requestId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatus(
        string requestId,
        CancellationToken cancellationToken)
    {
        var result = await _statusHandler.HandleAsync(
            requestId,
            cancellationToken);

        if (result is null)
        {
            return NotFound(new
            {
                message = $"Weather request '{requestId}' was not found."
            });
        }

        return Ok(result);
    }

    [HttpGet("{requestId}/results")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetResults(
        string requestId,
        CancellationToken cancellationToken)
    {
        var result = await _resultsHandler.HandleAsync(
            requestId,
            cancellationToken);

        if (result is null)
        {
            return NotFound(new
            {
                message = $"Weather request '{requestId}' was not found."
            });
        }

        return Ok(result);
    }
}
