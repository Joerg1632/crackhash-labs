using System.Text;
using System.Text.Json;
using Worker.DTOs;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Worker.Infrastructure.RabbitMQ;

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
                    exchange: settings.ResultsExchange,
                    type: ExchangeType.Direct,
                    durable: true).GetAwaiter().GetResult();
                
                channel.QueueDeclareAsync(
                    queue: settings.ResultsQueue,
                    durable: true,
                    exclusive: false,
                    autoDelete: false).GetAwaiter().GetResult();
                
                channel.QueueBindAsync(
                    queue: settings.ResultsQueue,
                    exchange: settings.ResultsExchange,
                    routingKey: settings.ResultsRoutingKey).GetAwaiter().GetResult();
                
                Console.WriteLine("Connected to RabbitMQ");
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"RabbitMQ not ready, attempt {i + 1}/10. Waiting 3s... {ex.Message}");
                Thread.Sleep(3000);
            }
        }
    
        throw new Exception("Could not connect to RabbitMQ after 10 attempts");
    }

    public async Task PublishResultAsync(ReportDto report)
    {
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(report));
        var props = new BasicProperties
        {
            Persistent = true
        };

        await channel.BasicPublishAsync(
            exchange: settings.ResultsExchange,
            routingKey: settings.ResultsRoutingKey,
            mandatory: false,
            basicProperties: props,
            body: body);
    }
    
    public void Dispose()
    {
        channel.CloseAsync().GetAwaiter().GetResult();
        connection.CloseAsync().GetAwaiter().GetResult();
    }
}