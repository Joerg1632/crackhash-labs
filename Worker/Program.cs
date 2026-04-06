using System.Text.Json;
using Worker.Services;
using System.Text.Json.Serialization;
using Scalar.AspNetCore;
using Worker.Infrastructure.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.Configure<RabbitMqSettings>(
    builder.Configuration.GetSection("RabbitMQ"));

builder.Services.AddSingleton<RabbitMqPublisher>();
builder.Services.AddSingleton<ICrackService, CrackWorkerService>();
builder.Services.AddHostedService<RabbitMqConsumer>();
builder.Services.AddOpenApi();

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("CrackHash Worker API")
            .WithTheme(ScalarTheme.Moon)
            .WithSidebar(true);
    });
}

app.UseHttpsRedirection();
app.UseRouting();
app.MapControllers();

app.Run();