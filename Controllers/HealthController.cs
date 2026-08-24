using Microsoft.AspNetCore.Mvc;
using thermometrum_backend.Ingest;
using thermometrum_backend.Storage;

namespace thermometrum_backend.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController : ControllerBase
{
    private readonly ReadingRepository _repository;
    private readonly MqttIngestService _ingest;

    public HealthController(ReadingRepository repository, MqttIngestService ingest)
    {
        _repository = repository;
        _ingest = ingest;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var clickHouse = await _repository.IsReachableAsync(cancellationToken);
        var mqtt = _ingest.IsConnected;
        var status = new { clickHouse, mqtt };

        return clickHouse && mqtt ? Ok(status) : StatusCode(StatusCodes.Status503ServiceUnavailable, status);
    }
}
