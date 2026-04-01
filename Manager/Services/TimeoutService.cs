namespace Manager.Services;

public class TimeoutService : BackgroundService
{
    private readonly ICrackManagerService crackManagerService;

    public TimeoutService(ICrackManagerService manager)
    {
        crackManagerService = manager;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            crackManagerService.CheckTimeouts();
            await Task.Delay(1000, stoppingToken);
        }
    }
}