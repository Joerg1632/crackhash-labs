using System.Text;
using System.Text.Json;
using Manager.DTOs;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Manager.Infrastructure.RabbitMQ;

public class RabbitMqPublisher : IDisposable
{
    private readonly IConnection connection;
    private readonly IChannel channel;
    private readonly RabbitMqSettings settings;
    private readonly ILogger<RabbitMqPublisher> logger;
    
    public RabbitMqPublisher(IOptions<RabbitMqSettings> options, ILogger<RabbitMqPublisher> logger)
    {
        this.logger = logger;
        settings = options.Value;
        var factory = new ConnectionFactory { HostName = settings.Host };
    
        for (var i = 0; i < 10; i++)
        {
            try
            {
                connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
                channel = connection.CreateChannelAsync().GetAwaiter().GetResult();

                channel.ExchangeDeclareAsync(
                    exchange: settings.TasksExchange,
                    type: ExchangeType.Direct,
                    durable: true).GetAwaiter().GetResult();

                channel.QueueDeclareAsync(
                    queue: settings.TaskQueue,
                    durable: true,
                    exclusive: false,
                    autoDelete: false).GetAwaiter().GetResult();

                channel.QueueBindAsync(
                    queue: settings.TaskQueue,
                    exchange: settings.TasksExchange,
                    routingKey: settings.TasksRoutingKey).GetAwaiter().GetResult();
            
                Console.WriteLine("Connected to RabbitMQ");
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"RabbitMQ not ready {ex.Message}");
                Thread.Sleep(3000);
            }
        }
    
        throw new Exception("Could not connect to RabbitMQ");
    }

    public async Task PublishTaskAsync(IEnumerable<TaskMessage> tasks)
    {
        foreach (var task in tasks)
        {
            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(task));
            var props = new BasicProperties
            {
                Persistent = true
            };

            await channel.BasicPublishAsync(
                exchange: settings.TasksExchange,
                routingKey: settings.TasksRoutingKey,
                mandatory: false,
                basicProperties: props,
                body: body);
        }
    }
    
    public async Task<bool> IsTasksQueueEmptyAsync()
    {
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("Authorization", "Basic " +
                                                              Convert.ToBase64String("guest:guest"u8.ToArray()));
        
        var response = await httpClient.GetStringAsync(
            $"http://rabbitmq:15672/api/queues/%2F/{settings.TaskQueue}");
        var queue = JsonSerializer.Deserialize<JsonElement>(response);
        var messages = queue.GetProperty("messages").GetInt32();
        
        logger.LogInformation("Tasks queue has {Count} messages", messages);
        return messages == 0;
    }
    
    public void Dispose()
    {
        channel.CloseAsync().GetAwaiter().GetResult();
        connection.CloseAsync().GetAwaiter().GetResult();
    }
}