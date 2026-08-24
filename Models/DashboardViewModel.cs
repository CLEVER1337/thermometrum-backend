namespace thermometrum_backend.Models;

public sealed record DashboardViewModel(
    IReadOnlyList<DeviceSummaryDto> Devices,
    DeviceSummaryDto? Selected,
    bool StorageAvailable)
{
    public int Hours => 24;

    public int BucketMinutes => 5;
}
