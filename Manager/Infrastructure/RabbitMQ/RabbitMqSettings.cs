namespace Manager.Infrastructure.RabbitMQ;

public class RabbitMqSettings
{
    public string Host { get; set; } = "rabbitmq";
    public string TaskQueue { get; set; } = "tasks";
    public string ResultsQueue { get; set; } = "results";
    public string TasksExchange { get; set; } = "tasks-exchange";
    public string ResultsExchange { get; set; } = "results-exchange";
    public string TasksRoutingKey { get; set; } = "task";
    public string ResultsRoutingKey { get; set; } = "result";
}