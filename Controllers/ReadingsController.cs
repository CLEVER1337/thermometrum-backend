using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using thermometrum_backend.Models;
using thermometrum_backend.Storage;

namespace thermometrum_backend.Controllers;

[ApiController]
[Route("api/readings")]
public sealed class ReadingsController : ControllerBase
{
    private readonly ReadingRepository _repository;

    public ReadingsController(ReadingRepository repository)
    {
        _repository = repository;
    }

    [HttpGet("latest")]
    public async Task<ActionResult<LatestReadingDto>> GetLatest(
        [Required][StringLength(64, MinimumLength = 1)] string deviceId,
        CancellationToken cancellationToken)
    {
        var reading = await _repository.GetLatestAsync(deviceId, cancellationToken);
        return reading is null ? NotFound() : reading;
    }

    [HttpGet("history")]
    public Task<IReadOnlyList<HistoryPointDto>> GetHistory(
        [Required][StringLength(64, MinimumLength = 1)] string deviceId,
        CancellationToken cancellationToken,
        [Range(1, 168)] int hours = 24,
        [Range(1, 60)] int bucketMinutes = 5) =>
        _repository.GetHistoryAsync(deviceId, hours, bucketMinutes, cancellationToken);
}
