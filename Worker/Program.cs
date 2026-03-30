using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Worker.Controllers;
using Worker.Services;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddSingleton<ICrackService, CrackWorkerService>();
builder.Services.AddHttpClient();
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