using System.Text.Json;
using System.Text.Json.Serialization;
using Manager;
using Manager.Cache;
using Manager.Core.Dispatch;
using Manager.Core.Tasks;
using Manager.Core.Timeout;
using Manager.Core.Workers;
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

builder.Services.AddHttpClient();

builder.Services.Configure<CrackManagerSettings>(
    builder.Configuration.GetSection("CrackManager"));

builder.Services.AddSingleton<Cache>();
builder.Services.AddSingleton<TaskSplitter>();
builder.Services.AddSingleton<WorkerProbe>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<WorkerProbe>());
builder.Services.AddSingleton<WorkerDispatcher>();
builder.Services.AddSingleton<TimeoutManager>();
builder.Services.AddSingleton<ICrackManagerService, CrackManagerService>();
builder.Services.AddHostedService<TimeoutService>();

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

app.Run();