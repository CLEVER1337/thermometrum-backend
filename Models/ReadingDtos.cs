namespace thermometrum_backend.Models;

public sealed record DeviceSummaryDto(string DeviceId, DateTimeOffset LastSeen, float Temperature, float Humidity);

public sealed record LatestReadingDto(string DeviceId, DateTimeOffset Timestamp, float Temperature, float Humidity);

public sealed record HistoryPointDto(DateTimeOffset Timestamp, float Temperature, float Humidity);
