namespace thermometrum_backend.Configuration;

public sealed class ClickHouseOptions
{
    public const string SectionName = "ClickHouse";

    public string ConnectionString { get; set; } = "Host=localhost;Port=8123;Database=thermometrum;Username=default";

    public string TableName { get; set; } = "thermometrum.readings";

    public int BatchSize { get; set; } = 1000;

    public TimeSpan FlushInterval { get; set; } = TimeSpan.FromSeconds(2);

    public int QueueCapacity { get; set; } = 10_000;
}
