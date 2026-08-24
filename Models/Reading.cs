namespace thermometrum_backend.Models;

public sealed record Reading(string DeviceId, DateTime Timestamp, float Temperature, float Humidity);
