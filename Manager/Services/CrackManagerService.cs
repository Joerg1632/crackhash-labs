using System.Collections.Concurrent;
using Manager.Cache;
using Manager.DTOs;
using Manager.Enums;
using Manager.Models;

namespace Manager.Services;

public class CrackManagerService
{
    private readonly IHttpClientFactory httpClientFactory;
    private readonly ILogger<CrackManagerService> logger;
    private readonly Cache.Cache cache;
    private readonly ConcurrentQueue<RequestState> requestQueue = new();
    private readonly object locker = new();
    RequestState? currentRequest;
    private readonly string[] workerUrls;

    private const string Alphabet = "abcdefghijklmnopqrstuvwxyz0123456789";
    private static readonly TimeSpan WorkerTimeout = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan TotalTimeout = TimeSpan.FromMinutes(5);

    public CrackManagerService(
        IHttpClientFactory factory,
        IConfiguration config,
        Cache.Cache cache,
        ILogger<CrackManagerService> logger)
    {
        httpClientFactory = factory;
        this.cache = cache;
        this.logger = logger;
        workerUrls = config.GetSection("WorkerUrls").Get<string[]>() ?? new[] { "http://worker:80" };
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
            TryDispatchTasks(
                requestId,
                new CrackRequest(state.Hash, state.MaxLength),
                state);
        }
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
        var now = DateTime.UtcNow;

        lock (state)
        {
            foreach (var (workerId, rangeTask) in state.InProgress)
            {
                if (rangeTask.StartedAt != null && now - rangeTask.StartedAt > WorkerTimeout)
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
                    }
                }
            }
            
            TryDispatchTasks(
                state.requestId,
                new CrackRequest(state.Hash, state.MaxLength),
                state);

            if (now - state.CreatedAt > TotalTimeout)
            {
                state.Status = RequestStatus.PARTIAL_READY;
                
                currentRequest = null;
                TryStartNext();
            }
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
    
    private void TryStartNext()
    {
        lock (locker)
        {
            if (currentRequest != null)
                return;

            if (!requestQueue.TryDequeue(out var next))
                return;
            
            currentRequest = next;
            next.WorkerAlive = ProbeWorkers();
            var activeWorkers = next.WorkerAlive.Count(x => x);
            
            if (activeWorkers == 0)
            {
                next.Status = RequestStatus.ERROR;
                currentRequest = null;
                return;
            }
            
            EnqueueBalancedRanges(
                next.requestId,
                next,
                CalculateTotalCombinations(Alphabet.Length, next.MaxLength),
                activeWorkers);
            
            TryDispatchTasks(
                next.requestId,
                new CrackRequest(next.Hash, next.MaxLength),
                next);
        }
    }

    private void TryDispatchTasks(string requestId, CrackRequest request, RequestState state)
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
                requestId,
                idleWorker.Value,
                task.Start,
                task.Count,
                state.PendingTasks.Count,
                state.InProgress.Count);

            var workerTask = new WorkerTaskDto(
                requestId,
                request.Hash,
                request.MaxLength,
                task.Start,
                task.Count,
                idleWorker.Value,
                Alphabet);

            _ = client.PostAsJsonAsync(
                $"{workerUrls[idleWorker.Value]}/internal/api/worker/hash/crack/task",
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
                    requestId,
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

    private void EnqueueBalancedRanges(string requestId, RequestState state, long total, int workersCount)
    {
        long currentStart = 0;
        var baseChunk = total / workersCount;
        var remainder = total % workersCount;

        for (var i = 0; i < workersCount; i++)
        {
            var count = baseChunk + (i < remainder ? 1 : 0);
            if (count <= 0)
                continue;

            var task = new RangeTask
            {
                Start = currentStart,
                Count = count
            };

            state.PendingTasks.Enqueue(task);
            logger.LogInformation(
                "Request {RequestId}: [CREATE] range {Start}-{Count} by workers={WorkersCount}",
                requestId,
                task.Start,
                task.Count,
                workersCount);

            currentStart += count;
        }
    }

    private bool[] ProbeWorkers()
    {
        var result = new bool[workerUrls.Length];
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(2);

        for (var workerId = 0; workerId < workerUrls.Length; workerId++)
        {
            try
            {
                var response = client.GetAsync(
                    $"{workerUrls[workerId]}/internal/api/worker/hash/crack/health")
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

    private static long CalculateTotalCombinations(int alphabetSize, int maxLength)
    {
        long totalCombinations = 0;
        long alphabetPower = 1;

        for (var len = 1; len <= maxLength; len++)
        {
            alphabetPower *= alphabetSize;
            totalCombinations += alphabetPower;

            if (alphabetPower < 0 || totalCombinations < 0)
                return long.MaxValue;
        }

        return totalCombinations;
    }
}