using ClickHouse.Client.ADO;
using ClickHouse.Client.Copy;
using Microsoft.Extensions.Options;
using thermometrum_backend.Configuration;
using thermometrum_backend.Models;
using thermometrum_backend.Storage;

namespace thermometrum_backend.Ingest;

public sealed class ClickHouseWriter : BackgroundService
{
    private static readonly string[] Columns = ["device_id", "ts", "temperature", "humidity"];
    private const int MaxAttempts = 5;

    private readonly ReadingQueue _queue;
    private readonly ClickHouseConnectionSource _connectionSource;
    private readonly ClickHouseOptions _options;
    private readonly ILogger<ClickHouseWriter> _logger;

    private ClickHouseConnection? _connection;
    private ClickHouseBulkCopy? _bulkCopy;

    public ClickHouseWriter(
        ReadingQueue queue,
        ClickHouseConnectionSource connectionSource,
        IOptions<ClickHouseOptions> options,
        ILogger<ClickHouseWriter> logger)
    {
        _queue = queue;
        _connectionSource = connectionSource;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var batch = new List<Reading>(_options.BatchSize);

        try
        {
            while (await _queue.Reader.WaitToReadAsync(stoppingToken))
            {
                await FillBatchAsync(batch, stoppingToken);

                if (batch.Count > 0)
                {
                    await WriteBatchAsync(batch, stoppingToken);
                    batch.Clear();
                }
            }
        }
        catch (OperationCanceledException)
        {
        }

        while (_queue.Reader.TryRead(out var reading))
        {
            batch.Add(reading);
        }

        if (batch.Count > 0)
        {
            await WriteBatchAsync(batch, CancellationToken.None);
        }
    }

    private async Task FillBatchAsync(List<Reading> batch, CancellationToken stoppingToken)
    {
        using var window = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        window.CancelAfter(_options.FlushInterval);

        try
        {
            while (batch.Count < _options.BatchSize)
            {
                while (batch.Count < _options.BatchSize && _queue.Reader.TryRead(out var reading))
                {
                    batch.Add(reading);
                }

                if (batch.Count >= _options.BatchSize || !await _queue.Reader.WaitToReadAsync(window.Token))
                {
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task WriteBatchAsync(List<Reading> batch, CancellationToken cancellationToken)
    {
        var rows = new object[batch.Count][];
        for (var i = 0; i < batch.Count; i++)
        {
            var reading = batch[i];
            rows[i] = [reading.DeviceId, reading.Timestamp, reading.Temperature, reading.Humidity];
        }

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                var bulkCopy = await GetBulkCopyAsync();
                await bulkCopy.WriteToServerAsync(rows, cancellationToken);
                _logger.LogDebug("Wrote {Count} readings to {Table}", rows.Length, _options.TableName);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                ResetConnection();

                if (attempt == MaxAttempts)
                {
                    _logger.LogError(exception, "Giving up on {Count} readings after {Attempts} attempts", rows.Length, MaxAttempts);
                    return;
                }

                var delay = TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, attempt)));
                _logger.LogWarning(exception, "Insert of {Count} readings failed, retrying in {Delay}", rows.Length, delay);

                try
                {
                    await Task.Delay(delay, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    private async Task<ClickHouseBulkCopy> GetBulkCopyAsync()
    {
        if (_bulkCopy is not null)
        {
            return _bulkCopy;
        }

        _connection = _connectionSource.Create();
        var bulkCopy = new ClickHouseBulkCopy(_connection)
        {
            DestinationTableName = _options.TableName,
            ColumnNames = Columns,
            BatchSize = _options.BatchSize,
        };

        await bulkCopy.InitAsync();
        _bulkCopy = bulkCopy;
        return bulkCopy;
    }

    private void ResetConnection()
    {
        _bulkCopy?.Dispose();
        _bulkCopy = null;
        _connection?.Dispose();
        _connection = null;
    }

    public override void Dispose()
    {
        ResetConnection();
        base.Dispose();
    }
}
