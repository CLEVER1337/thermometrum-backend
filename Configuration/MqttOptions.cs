namespace thermometrum_backend.Configuration;

public sealed class MqttOptions
{
    public const string SectionName = "Mqtt";

    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 1883;

    public string ClientId { get; set; } = "thermometrum-backend";

    public string TopicFilter { get; set; } = "thermometrum/+/reading";

    public string? Username { get; set; }

    public string? Password { get; set; }

    public TimeSpan ReconnectDelay { get; set; } = TimeSpan.FromSeconds(5);
}
