using System.Text.Json;
using System.Text.Json.Serialization;
using Manager;
using Manager.Core.Tasks;
using Manager.Infrastructure.Mongo;
using Manager.Infrastructure.RabbitMQ;
using Manager.Services;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.Configure<CrackManagerSettings>(
    builder.Configuration.GetSection("CrackManager"));
builder.Services.Configure<RabbitMqSettings>(
    builder.Configuration.GetSection("RabbitMQ"));
builder.Services.Configure<MongoSettings>(
    builder.Configuration.GetSection("MongoDB"));

builder.Services.AddSingleton<MongoRequestRepository>();
builder.Services.AddSingleton<RabbitMqPublisher>();
builder.Services.AddHostedService<RabbitMqConsumer>();
builder.Services.AddSingleton<TaskSplitter>();
builder.Services.AddSingleton<ICrackManagerService, CrackManagerService>();

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("CrackHash Manager API")
            .WithTheme(ScalarTheme.Moon)
            .WithSidebar(true);
    });
}

app.UseHttpsRedirection();
app.UseRouting();
app.MapControllers();
app.UseCors(policy => policy
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader());

var crackManager = app.Services.GetRequiredService<ICrackManagerService>();
await crackManager.RecoverInProgressRequestsAsync();

app.Run();