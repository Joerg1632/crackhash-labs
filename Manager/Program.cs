var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

var group = app.MapGroup("/api/hash");

group.MapPost("/crack", () =>
{
    /*отправить запрос на взлом хэша*/
});

group.MapGet("/status", () => {/*получить статус*/});

app.Run();
