using Microsoft.Extensions.Options;

namespace Manager.Core.Workers;

public class WorkerProbe : IHostedService
{
    private readonly IHttpClientFactory httpClientFactory;
    private readonly CrackManagerSettings settings;
    private readonly PeriodicTimer timer = new(TimeSpan.FromSeconds(5));
    private volatile bool[] workerAlive;

    public WorkerProbe(IHttpClientFactory httpClientFactory, IOptions<CrackManagerSettings> options)
    {
        this.httpClientFactory = httpClientFactory;
        settings = options.Value;
        workerAlive = new bool[settings.WorkerUrls.Length];
    }

    public bool[] GetCachedState() => workerAlive;

    public bool[] ProbeWorkers()
    {
        var result = new bool[settings.WorkerUrls.Length];
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(2);

        for (var workerId = 0; workerId < settings.WorkerUrls.Length; workerId++)
        {
            try
            {
                var response = client.GetAsync(
                        $"{settings.WorkerUrls[workerId]}/internal/api/worker/hash/crack/health")
                    .GetAwaiter()
                    .GetResult();

                result[workerId] = response.IsSuccessStatusCode;
            }
            catch
            {
                result[workerId] = false;
            }
        }

        return result;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        workerAlive = ProbeWorkers();
        _ = Task.Run(async () =>
        {
            while (await timer.WaitForNextTickAsync(ct))
                workerAlive = ProbeWorkers();
        }, ct);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}