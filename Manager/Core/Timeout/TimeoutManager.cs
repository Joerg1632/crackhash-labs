using Manager.Core.Dispatch;
using Manager.DTOs;
using Manager.Enums;
using Manager.Models;
using Microsoft.Extensions.Options;

namespace Manager.Core.Timeout;

public class TimeoutManager
{
    private readonly ILogger<TimeoutManager> logger;
    private readonly WorkerDispatcher workerDispatcher;
    private readonly CrackManagerSettings settings;

    public TimeoutManager(
        ILogger<TimeoutManager> logger,
        WorkerDispatcher workerDispatcher,
        IOptions<CrackManagerSettings> options)
    {
        this.logger = logger;
        this.workerDispatcher = workerDispatcher;
        settings = options.Value;
    }
    
    public bool CheckTimeouts(RequestState state)
    {
        var now = DateTime.UtcNow;
        var shouldStartNext = false;
        var needDispatch = false;

        lock (state)
        {
            foreach (var (workerId, rangeTask) in state.InProgress)
            {
                if (rangeTask.StartedAt != null 
                    && now - rangeTask.StartedAt > TimeSpan.FromSeconds(settings.WorkerTimeoutSeconds))
                {
                    if (state.InProgress.TryRemove(workerId, out var failedTask))
                    {
                        failedTask.WorkerId = null;
                        failedTask.StartedAt = null;
                        state.PendingTasks.Enqueue(failedTask);
                        
                        if (workerId >= 0 && workerId < state.WorkerAlive.Length)
                            state.WorkerAlive[workerId] = false;
                        
                        logger.LogWarning(
                            "Request {RequestId}: [TIMEOUT] worker {WorkerId} lost {Start}-{Count}",
                            state.requestId,
                            workerId,
                            failedTask.Start,
                            failedTask.Count);

                        needDispatch = true;
                    }
                }
            }

            if (now - state.CreatedAt > TimeSpan.FromSeconds(settings.TotalTimeoutSeconds))
            {
                state.Status = RequestStatus.PARTIAL_READY;
                shouldStartNext = true;
            }
        }
        
        if (needDispatch && !shouldStartNext)
        {
            workerDispatcher.TryDispatchTasks(
                new CrackRequest(state.Hash, state.MaxLength),
                state);
        }
        
        return shouldStartNext;
    }

}