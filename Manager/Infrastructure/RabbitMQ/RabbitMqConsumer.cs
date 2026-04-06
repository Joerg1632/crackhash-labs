using System.Text;
using System.Text.Json;
using Manager.DTOs;
using Manager.Services;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Manager.Infrastructure.RabbitMQ;

public class RabbitMqConsumer : BackgroundService
{
    private readonly RabbitMqSettings settings;
    private readonly ICrackManagerService crackManagerService;
    private readonly ILogger<RabbitMqConsumer> logger;
    private IConnection? connection;
    private IChannel? channel;

    public RabbitMqConsumer(
        IOptions<RabbitMqSettings> options,
        ICrackManagerService crackManagerService,
        ILogger<RabbitMqConsumer> logger)
    {
        settings = options.Value;
        this.crackManagerService = crackManagerService;
        this.logger = logger;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("RabbitMqConsumer starting, connecting to {Host}", settings.Host);
        var factory = new ConnectionFactory { HostName = settings.Host };
        connection = await factory.CreateConnectionAsync(stoppingToken);
        channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await channel.ExchangeDeclareAsync(
            exchange: settings.ResultsExchange,
            type: ExchangeType.Direct,
            durable: true,
            cancellationToken: stoppingToken);

        await channel.QueueDeclareAsync(
            queue: settings.ResultsQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: stoppingToken);
        
        await channel.QueueBindAsync(
            queue: settings.ResultsQueue,
            exchange: settings.ResultsExchange,
            routingKey: settings.ResultsRoutingKey,
            cancellationToken: stoppingToken);
        
        logger.LogInformation(
            "Listening on queue={Queue}, exchange={Exchange}, routingKey={RoutingKey}",
            settings.ResultsQueue,
            settings.ResultsExchange,
            settings.ResultsRoutingKey);

        await channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: 1,
            global: false,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                logger.LogInformation("Received message from results queue");
                var body = Encoding.UTF8.GetString(ea.Body.ToArray());
                logger.LogInformation("Body: {Body}", body);
        
                var report = JsonSerializer.Deserialize<ReportDto>(body);
                if (report != null)
                    await crackManagerService.ReportResultAsync(report.RequestId, report.FoundWords ?? []);

                await channel.BasicAckAsync(ea.DeliveryTag, false);
                logger.LogInformation("Acked message");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process result message");
                await channel.BasicNackAsync(ea.DeliveryTag, false, true);
            }
        };
        
        await channel.BasicConsumeAsync(
            queue: settings.ResultsQueue,
            autoAck: false,
            consumer: consumer);
            
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        if (channel != null) 
            await channel.CloseAsync(cancellationToken);
        if (connection != null) 
            await connection.CloseAsync(cancellationToken);
    }
}