using Microsoft.AspNetCore.Mvc;
using thermometrum_backend.Models;
using thermometrum_backend.Storage;

namespace thermometrum_backend.Controllers;

[ApiController]
[Route("api/devices")]
public sealed class DevicesController : ControllerBase
{
    private readonly ReadingRepository _repository;

    public DevicesController(ReadingRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    public Task<IReadOnlyList<DeviceSummaryDto>> Get(CancellationToken cancellationToken) =>
        _repository.GetDevicesAsync(cancellationToken);
}
