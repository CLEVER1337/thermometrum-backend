using System.Data.Common;
using ClickHouse.Client.ADO.Parameters;
using Microsoft.Extensions.Options;
using thermometrum_backend.Configuration;
using thermometrum_backend.Models;

namespace thermometrum_backend.Storage;

public sealed class ReadingRepository
{
    private readonly ClickHouseConnectionSource _connectionSource;
    private readonly string _table;

    public ReadingRepository(ClickHouseConnectionSource connectionSource, IOptions<ClickHouseOptions> options)
    {
        _connectionSource = connectionSource;
        _table = options.Value.TableName;
    }

    public async Task<IReadOnlyList<DeviceSummaryDto>> GetDevicesAsync(CancellationToken cancellationToken)
    {
        await using var connection = _connectionSource.Create();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT device_id,
                   max(ts) AS last_seen,
                   argMax(temperature, ts) AS temperature,
                   argMax(humidity, ts) AS humidity
            FROM {_table}
            GROUP BY device_id
            ORDER BY device_id
            """;

        var devices = new List<DeviceSummaryDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            devices.Add(new DeviceSummaryDto(
                reader.GetString(0),
                ReadUtc(reader, 1),
                ReadFloat(reader, 2),
                ReadFloat(reader, 3)));
        }

        return devices;
    }

    public async Task<LatestReadingDto?> GetLatestAsync(string deviceId, CancellationToken cancellationToken)
    {
        await using var connection = _connectionSource.Create();
        using var command = connection.CreateCommand();
        command.CommandText = $$"""
            SELECT device_id, ts, temperature, humidity
            FROM {{_table}}
            WHERE device_id = {deviceId:String}
            ORDER BY ts DESC
            LIMIT 1
            """;
        AddParameter(command, "deviceId", "String", deviceId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new LatestReadingDto(
            reader.GetString(0),
            ReadUtc(reader, 1),
            ReadFloat(reader, 2),
            ReadFloat(reader, 3));
    }

    public async Task<IReadOnlyList<HistoryPointDto>> GetHistoryAsync(
        string deviceId,
        int hours,
        int bucketMinutes,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionSource.Create();
        using var command = connection.CreateCommand();
        command.CommandText = $$"""
            SELECT toStartOfInterval(ts, toIntervalMinute({bucketMinutes:UInt16})) AS bucket,
                   avg(temperature) AS temperature,
                   avg(humidity) AS humidity
            FROM {{_table}}
            WHERE device_id = {deviceId:String}
              AND ts >= now() - toIntervalHour({hours:UInt16})
            GROUP BY bucket
            ORDER BY bucket
            """;
        AddParameter(command, "deviceId", "String", deviceId);
        AddParameter(command, "hours", "UInt16", (ushort)hours);
        AddParameter(command, "bucketMinutes", "UInt16", (ushort)bucketMinutes);

        var points = new List<HistoryPointDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            points.Add(new HistoryPointDto(
                ReadUtc(reader, 0),
                ReadFloat(reader, 1),
                ReadFloat(reader, 2)));
        }

        return points;
    }

    public async Task<bool> IsReachableAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = _connectionSource.Create();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            await command.ExecuteScalarAsync(cancellationToken);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static void AddParameter(DbCommand command, string name, string clickHouseType, object value)
    {
        var parameter = (ClickHouseDbParameter)command.CreateParameter();
        parameter.ParameterName = name;
        parameter.ClickHouseType = clickHouseType;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static DateTimeOffset ReadUtc(DbDataReader reader, int ordinal)
    {
        var value = reader.GetDateTime(ordinal);
        return new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }

    private static float ReadFloat(DbDataReader reader, int ordinal) => Convert.ToSingle(reader.GetValue(ordinal));
}
