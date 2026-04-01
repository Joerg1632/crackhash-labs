using System.Collections.Concurrent;
using Manager.Core.Dispatch;
using Manager.Core.Workers;
using Manager.Core.Tasks;
using Manager.Core.Timeout;
using Manager.DTOs;
using Manager.Enums;
using Manager.Models;
using Microsoft.Extensions.Options;

namespace Manager.Services;

public class CrackManagerService : ICrackManagerService
{
    private readonly ILogger<CrackManagerService> logger;
    private readonly Cache.Cache cache;
    private readonly ConcurrentQueue<RequestState> requestQueue = new();
    private readonly TaskSplitter taskSplitter;
    private readonly WorkerDispatcher workerDispatcher;
    private readonly TimeoutManager timeoutManager;
    private readonly WorkerProbe workerProbe;
    private readonly CrackManagerSettings  settings;
    private readonly object locker = new();
    RequestState? currentRequest;

    public CrackManagerService(
        Cache.Cache cache,
        ILogger<CrackManagerService> logger,
        IOptions<CrackManagerSettings> options, 
        TaskSplitter taskSplitter, 
        WorkerDispatcher workerDispatcher, 
        TimeoutManager timeoutManager, 
        WorkerProbe workerProbe)
    {
        this.cache = cache;
        this.logger = logger;
        this.taskSplitter = taskSplitter;
        this.workerDispatcher = workerDispatcher;
        this.timeoutManager = timeoutManager;
        this.workerProbe = workerProbe;
        settings = options.Value;
    }

    public string StartCrack(CrackRequest request)
    {
        if (cache.TryGetByHashAndLength(request.Hash, request.MaxLength, out var cachedRequestId, out _)
            && cachedRequestId is not null)
        {
            logger.LogInformation(
                "Cache hit for hash request. Returning existing requestId {RequestId}",
                cachedRequestId);
            return cachedRequestId;
        }
        
        var requestId = Guid.NewGuid().ToString();

        var state = new RequestState
        {
            requestId = requestId,
            Hash = request.Hash,
            MaxLength = request.MaxLength,
            Status = RequestStatus.IN_PROGRESS,
            CreatedAt = DateTime.UtcNow
        };

        requestQueue.Enqueue(state);
        TryStartNext();

        return requestId;
    }

    public void ReportResult(string requestId, List<string> words, int workerId)
    {
        if (currentRequest == null || currentRequest.requestId != requestId)
            return;

        var state = currentRequest;
        var needDispatch = false;

        lock (state)
        {
            if (!state.InProgress.TryRemove(workerId, out var task))
                return;
            
            state.FoundWords.AddRange(words);
            task.Completed = true;

            if (workerId >= 0 && workerId < state.WorkerAlive.Length)
            {
                state.WorkerAlive[workerId] = true;

                logger.LogInformation(
                    "Request {RequestId}: [DONE] worker {WorkerId} finished {Start}-{Count}. Pending={Pending}, InProgress={InProgress}",
                    requestId,
                    workerId,
                    task.Start,
                    task.Count,
                    state.PendingTasks.Count,
                    state.InProgress.Count);
            }

            if (state.PendingTasks.IsEmpty && state.InProgress.IsEmpty)
            {
                state.Status = RequestStatus.READY;
                
                cache.Upsert(state.requestId, new CachedResult
                (
                    state.Hash,
                    state.MaxLength,
                    state.FoundWords,
                    DateTime.UtcNow
                ));
                currentRequest = null;
                TryStartNext();
                
                return;
            }
            
            needDispatch = true;
        }

        if (needDispatch)
        {
            workerDispatcher.TryDispatchTasks(
                new CrackRequest(state.Hash, state.MaxLength),
                state);
        }
    }
    
    public (RequestStatus Status, List<string>? Data) GetStatus(string requestId)
    {
        if (currentRequest != null && currentRequest.requestId == requestId)
            return (currentRequest.Status, currentRequest.FoundWords);

        return cache.TryGet(requestId, out var cachedResult) 
            ? (RequestStatus.READY, cachedResult.FoundWords) 
            : (RequestStatus.ERROR, null);
    }
    
    public void CheckTimeouts()
    {
        if (currentRequest == null)
            return;

        RequestState state;
        lock (locker)
        {
            state = currentRequest;
        }

        var shouldStartNext = timeoutManager.CheckTimeouts(state);

        if (shouldStartNext)
        {
            lock (locker)
            {
                currentRequest = null;
            }

            TryStartNext();
        }
    }
    
    private void TryStartNext()
    {
        lock (locker)
        {
            if (currentRequest != null)
                return;

            if (!requestQueue.TryDequeue(out var next))
                return;
            
            currentRequest = next;
            next.WorkerAlive = workerProbe.GetCachedState();
            var activeWorkers = next.WorkerAlive.Count(x => x);
            
            if (activeWorkers == 0)
            {
                next.Status = RequestStatus.ERROR;
                currentRequest = null;
                return;
            }
            
            taskSplitter.EnqueueBalancedRanges(
                next.requestId,
                next,
                taskSplitter.CalculateTotalCombinations(settings.Alphabet.Length, next.MaxLength),
                activeWorkers);
            
            workerDispatcher.TryDispatchTasks(
                new CrackRequest(next.Hash, next.MaxLength),
                next);
        }
    }
}