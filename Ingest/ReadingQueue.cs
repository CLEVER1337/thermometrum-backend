using System.Threading.Channels;
using Microsoft.Extensions.Options;
using thermometrum_backend.Configuration;
using thermometrum_backend.Models;

namespace thermometrum_backend.Ingest;

public sealed class ReadingQueue
{
    private readonly Channel<Reading> _channel;

    public ReadingQueue(IOptions<ClickHouseOptions> options)
    {
        _channel = Channel.CreateBounded<Reading>(new BoundedChannelOptions(options.Value.QueueCapacity)
        {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.DropWrite
        });
    }

    public ChannelReader<Reading> Reader => _channel.Reader;

    public bool TryEnqueue(Reading reading) => _channel.Writer.TryWrite(reading);
}
