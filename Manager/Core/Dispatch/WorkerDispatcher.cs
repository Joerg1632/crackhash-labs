using Manager.DTOs;
using Manager.Models;
using Microsoft.Extensions.Options;

namespace Manager.Core.Dispatch;

public class WorkerDispatcher
{
    private readonly ILogger<WorkerDispatcher> logger;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly CrackManagerSettings settings;

    public WorkerDispatcher(
        ILogger<WorkerDispatcher> logger, 
        IHttpClientFactory factory,
        IOptions<CrackManagerSettings> options)
    {
        this.logger = logger;
        httpClientFactory = factory;
        settings = options.Value;
    }
     
    public void TryDispatchTasks(CrackRequest request, RequestState state)
    {
        var client = httpClientFactory.CreateClient();

        while (true)
        {
            var idleWorker = FindIdleWorker(state);

            if (idleWorker is null)
                break;

            if (!state.PendingTasks.TryDequeue(out var task))
                break;

            task.WorkerId = idleWorker.Value;
            task.StartedAt = DateTime.UtcNow;
            state.InProgress[idleWorker.Value] = task;

            logger.LogInformation(
                "Request {RequestId}: [TAKE] worker {WorkerId} took {Start}-{Count}. Pending={Pending}, InProgress={InProgress}",
                state.requestId,
                idleWorker.Value,
                task.Start,
                task.Count,
                state.PendingTasks.Count,
                state.InProgress.Count);

            var workerTask = new WorkerTaskDto(
                state.requestId,
                request.Hash,
                request.MaxLength,
                task.Start,
                task.Count,
                idleWorker.Value,
                settings.Alphabet);

            _ = client.PostAsJsonAsync(
                $"{settings.WorkerUrls[idleWorker.Value]}/internal/api/worker/hash/crack/task",
                workerTask).ContinueWith(t =>
            {
                if (!t.IsFaulted)
                    return;

                lock (state)
                {
                    if (state.InProgress.TryRemove(idleWorker.Value, out var failedTask))
                    {
                        failedTask.WorkerId = null;
                        failedTask.StartedAt = null;
                        state.PendingTasks.Enqueue(failedTask);
                    }

                    if (idleWorker.Value >= 0 && idleWorker.Value < state.WorkerAlive.Length)
                        state.WorkerAlive[idleWorker.Value] = false;
                }

                logger.LogError(
                    t.Exception,
                    "Request {RequestId}: failed to send task {Start}-{Count} to worker {WorkerId}",
                    state.requestId,
                    task.Start,
                    task.Count,
                    idleWorker.Value);
            });
        }
    }
       
    private static int? FindIdleWorker(RequestState state)
    {
        for (var i = 0; i < state.WorkerAlive.Length; i++)
        {
            if (state.WorkerAlive[i] && !state.InProgress.ContainsKey(i))
                return i;
        }

        return null;
    }
}