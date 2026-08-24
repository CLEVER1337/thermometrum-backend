using ClickHouse.Client.ADO;
using Microsoft.Extensions.Options;
using thermometrum_backend.Configuration;

namespace thermometrum_backend.Storage;

public sealed class ClickHouseConnectionSource
{
    public const string HttpClientName = "clickhouse";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ClickHouseOptions _options;

    public ClickHouseConnectionSource(IHttpClientFactory httpClientFactory, IOptions<ClickHouseOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    public ClickHouseConnection Create() =>
        new(_options.ConnectionString, _httpClientFactory, HttpClientName);
}
