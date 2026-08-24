using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using thermometrum_backend.Models;
using thermometrum_backend.Storage;

namespace thermometrum_backend.Controllers;

public class HomeController : Controller
{
    private readonly ReadingRepository _repository;
    private readonly ILogger<HomeController> _logger;

    public HomeController(ReadingRepository repository, ILogger<HomeController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<IActionResult> Index(string? deviceId, CancellationToken cancellationToken)
    {
        IReadOnlyList<DeviceSummaryDto> devices;
        try
        {
            devices = await _repository.GetDevicesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Cannot read the devices from ClickHouse");
            return View(new DashboardViewModel([], null, StorageAvailable: false));
        }

        var selected = deviceId is null
            ? devices.FirstOrDefault()
            : devices.FirstOrDefault(device => device.DeviceId == deviceId);

        return View(new DashboardViewModel(devices, selected, StorageAvailable: true));
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
