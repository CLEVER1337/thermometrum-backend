using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;
using thermometrum_backend.Models;

namespace thermometrum_backend.Ingest;

public static class ReadingPayloadParser
{
    public const float MinTemperature = -90f;
    public const float MaxTemperature = 90f;
    public const float MinHumidity = 0f;
    public const float MaxHumidity = 100f;
    public const int MaxDeviceIdLength = 64;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static bool TryParse(
        string topic,
        ReadOnlySequence<byte> payload,
        DateTime receivedAt,
        out Reading? reading,
        out string? error)
    {
        reading = null;

        if (!TryReadDeviceId(topic, out var deviceId, out error))
        {
            return false;
        }

        Payload? parsed;
        try
        {
            var jsonReader = new Utf8JsonReader(payload);
            parsed = JsonSerializer.Deserialize<Payload>(ref jsonReader, SerializerOptions);
        }
        catch (JsonException exception)
        {
            error = $"payload is not valid json: {exception.Message}";
            return false;
        }

        if (parsed is null)
        {
            error = "payload is empty";
            return false;
        }

        if (!TryReadMeasurement(parsed.Temperature, MinTemperature, MaxTemperature, "temperature", out var temperature, out error))
        {
            return false;
        }

        if (!TryReadMeasurement(parsed.Humidity, MinHumidity, MaxHumidity, "humidity", out var humidity, out error))
        {
            return false;
        }

        var timestamp = parsed.Ts?.UtcDateTime ?? receivedAt;
        reading = new Reading(deviceId!, timestamp, temperature, humidity);
        error = null;
        return true;
    }

    private static bool TryReadDeviceId(string topic, out string? deviceId, out string? error)
    {
        deviceId = null;
        var segments = topic.Split('/');

        if (segments.Length < 2 || segments[1].Length == 0)
        {
            error = "topic carries no device id";
            return false;
        }

        if (segments[1].Length > MaxDeviceIdLength)
        {
            error = $"device id is longer than {MaxDeviceIdLength} characters";
            return false;
        }

        deviceId = segments[1];
        error = null;
        return true;
    }

    private static bool TryReadMeasurement(
        double? value,
        float min,
        float max,
        string name,
        out float measurement,
        out string? error)
    {
        measurement = 0f;

        if (value is null)
        {
            error = $"{name} is missing";
            return false;
        }

        if (double.IsNaN(value.Value) || double.IsInfinity(value.Value))
        {
            error = $"{name} is not a number";
            return false;
        }

        if (value.Value < min || value.Value > max)
        {
            error = $"{name} {value.Value} is outside {min}..{max}";
            return false;
        }

        measurement = (float)value.Value;
        error = null;
        return true;
    }

    private sealed record Payload(
        [property: JsonPropertyName("temperature")] double? Temperature,
        [property: JsonPropertyName("humidity")] double? Humidity,
        [property: JsonPropertyName("ts")] DateTimeOffset? Ts);
}
