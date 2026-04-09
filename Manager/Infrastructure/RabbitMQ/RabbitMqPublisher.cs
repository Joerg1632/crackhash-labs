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

    public RabbitMqPublisher(IOptions<RabbitMqSettings> options)
    {
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
    
    public void Dispose()
    {
        channel.CloseAsync().GetAwaiter().GetResult();
        connection.CloseAsync().GetAwaiter().GetResult();
    }
}