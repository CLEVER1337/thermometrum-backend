using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Protocol;
using thermometrum_backend.Configuration;

namespace thermometrum_backend.Ingest;

public sealed class MqttIngestService : BackgroundService
{
    private readonly ReadingQueue _queue;
    private readonly MqttOptions _options;
    private readonly ILogger<MqttIngestService> _logger;

    private IMqttClient? _client;

    public MqttIngestService(ReadingQueue queue, IOptions<MqttOptions> options, ILogger<MqttIngestService> logger)
    {
        _queue = queue;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConnected => _client?.IsConnected ?? false;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new MqttClientFactory();
        using var client = factory.CreateMqttClient();
        _client = client;

        client.ApplicationMessageReceivedAsync += OnApplicationMessageReceivedAsync;
        client.DisconnectedAsync += OnDisconnectedAsync;

        var clientOptions = BuildClientOptions();
        var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
            .WithTopicFilter(_options.TopicFilter, MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!client.IsConnected)
                {
                    await client.ConnectAsync(clientOptions, stoppingToken);
                    await client.SubscribeAsync(subscribeOptions, stoppingToken);
                    _logger.LogInformation(
                        "Subscribed to {TopicFilter} on {Host}:{Port}",
                        _options.TopicFilter,
                        _options.Host,
                        _options.Port);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Cannot reach the broker at {Host}:{Port}, retrying in {Delay}",
                    _options.Host,
                    _options.Port,
                    _options.ReconnectDelay);
            }

            try
            {
                await Task.Delay(_options.ReconnectDelay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        if (client.IsConnected)
        {
            await client.DisconnectAsync(new MqttClientDisconnectOptionsBuilder().Build(), CancellationToken.None);
        }

        _client = null;
    }

    private MqttClientOptions BuildClientOptions()
    {
        var builder = new MqttClientOptionsBuilder()
            .WithTcpServer(_options.Host, _options.Port)
            .WithClientId(_options.ClientId)
            .WithCleanSession(false);

        if (!string.IsNullOrEmpty(_options.Username))
        {
            builder = builder.WithCredentials(_options.Username, _options.Password ?? string.Empty);
        }

        return builder.Build();
    }

    private Task OnApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs args)
    {
        var topic = args.ApplicationMessage.Topic;

        if (!ReadingPayloadParser.TryParse(topic, args.ApplicationMessage.Payload, DateTime.UtcNow, out var reading, out var error))
        {
            _logger.LogWarning("Discarded a message on {Topic}: {Error}", topic, error);
            return Task.CompletedTask;
        }

        if (!_queue.TryEnqueue(reading!))
        {
            _logger.LogWarning("The reading queue is full, dropped a reading from {DeviceId}", reading!.DeviceId);
        }

        return Task.CompletedTask;
    }

    private Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs args)
    {
        if (args.ClientWasConnected)
        {
            _logger.LogWarning(args.Exception, "Disconnected from the broker: {Reason}", args.Reason);
        }

        return Task.CompletedTask;
    }
}
