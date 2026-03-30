namespace Manager.Services;

public class TimeoutService : BackgroundService
{
    private readonly CrackManagerService manager;

    public TimeoutService(CrackManagerService manager)
    {
        this.manager = manager;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            manager.CheckTimeouts();
            
            await Task.Delay(1000, stoppingToken);
        }
    }
}