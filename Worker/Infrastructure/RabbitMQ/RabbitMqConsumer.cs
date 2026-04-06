using System.Text;
using System.Text.Json;
using Worker.DTOs;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Worker.Services;

namespace Worker.Infrastructure.RabbitMQ;

public class RabbitMqConsumer : BackgroundService
{
    private readonly RabbitMqSettings settings;
    private readonly ICrackService crackService;
    private readonly RabbitMqPublisher publisher;
    private readonly ILogger<RabbitMqConsumer> logger;
    private IConnection? connection;
    private IChannel? channel;

    public RabbitMqConsumer(
        IOptions<RabbitMqSettings> options,
        ICrackService crackService,
        RabbitMqPublisher publisher,
        ILogger<RabbitMqConsumer> logger)
    {
        settings = options.Value;
        this.logger = logger;
        this.publisher = publisher;
        this.crackService = crackService;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory { HostName = settings.Host };
        connection = await factory.CreateConnectionAsync(stoppingToken);
        channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
        
        await channel.ExchangeDeclareAsync(
            exchange: settings.TasksExchange,
            type: ExchangeType.Direct,
            durable: true,
            cancellationToken : stoppingToken);
        
        await channel.QueueDeclareAsync(
            queue: settings.TaskQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: stoppingToken);
        
        await channel.QueueBindAsync(
            queue: settings.TaskQueue,
            exchange: settings.TasksExchange,
            routingKey: settings.TasksRoutingKey,
            cancellationToken: stoppingToken);

        await channel.BasicQosAsync(0, 1, false, stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                var body = Encoding.UTF8.GetString(ea.Body.ToArray());
                var task = JsonSerializer.Deserialize<WorkerTaskRequest>(body);

                if (task == null)
                {
                    await channel.BasicNackAsync(ea.DeliveryTag, false, false);
                    return;
                }

                logger.LogInformation(
                    "Request {RequestId}: recieved task {Start}-{Count}",
                    task.RequestId, task.StartIndex, task.Count);

                var foundWords = await crackService.CrackRangeAsync(
                    task.Hash,
                    task.MaxLength,
                    task.StartIndex,
                    task.Count,
                    task.Alphabet);

                await publisher.PublishResultAsync(new ReportDto
                {
                    RequestId = task.RequestId,
                    FoundWords = foundWords
                });

                await channel.BasicAckAsync(ea.DeliveryTag, false);

                logger.LogInformation(
                    "Request {RequestId}: task done, found {Count} words",
                    task.RequestId, foundWords);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process task");
                await channel.BasicNackAsync(ea.DeliveryTag, false, true);
            }
        };
        
        await channel.BasicConsumeAsync(
            queue: settings.TaskQueue,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);
        
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