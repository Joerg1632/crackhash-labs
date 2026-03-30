using System.Text.Json;
using Manager.Services;
using System.Text.Json.Serialization;
using Manager.Cache;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });
builder.Services.AddSingleton<CrackManagerService>();
builder.Services.AddHostedService<TimeoutService>();
builder.Services.AddHttpClient();
builder.Services.AddOpenApi();
builder.Services.AddSingleton<Cache>();

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