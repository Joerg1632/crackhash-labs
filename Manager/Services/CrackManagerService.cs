using System.Collections.Concurrent;
using System.Net.Http.Json;
using Manager.Cache;
using Manager.DTOs;
using Manager.Enums;

namespace Manager.Services;

public class CrackManagerService
{
    private readonly IHttpClientFactory httpClientFactory;
    private readonly ILogger<CrackManagerService> logger;
    private readonly ResultCache cache;
    private readonly string[] workerUrls;

    private const string Alphabet = "abcdefghijklmnopqrstuvwxyz0123456789";
    private static readonly TimeSpan WorkerTimeout = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan TotalTimeout = TimeSpan.FromMinutes(5);

    public CrackManagerService(
        IHttpClientFactory factory,
        IConfiguration config,
        ResultCache cache,
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
        var total = CalculateTotalCombinations(Alphabet.Length, request.MaxLength);
        var workerAlive = ProbeWorkers();
        var activeWorkers = workerAlive.Count(alive => alive);

        var state = new RequestState
        {
            Hash = request.Hash,
            MaxLength = request.MaxLength,
            WorkerAlive = workerAlive,
            Status = RequestStatus.IN_PROGRESS,
            CreatedAt = DateTime.UtcNow
        };

        cache.Upsert(requestId, state);

        lock (state)
        {
            if (activeWorkers == 0)
            {
                state.Status = RequestStatus.ERROR;
                logger.LogError("Request {RequestId}: no alive workers detected at startup", requestId);
                return requestId;
            }

            EnqueueBalancedRanges(requestId, state, total, activeWorkers, 0);
            TryDispatchTasks(requestId, request, state);
        }

        return requestId;
    }

    public void ReportResult(string requestId, List<string> words, int workerId)
    {
        if (!cache.TryGet(requestId, out var state) || state is null)
            return;

        lock (state)
        {
            state.FoundWords.AddRange(words);

            if (state.InProgress.TryRemove(workerId, out var task))
            {
                task.Completed = true;
                if (workerId >= 0 && workerId < state.WorkerAlive.Length)
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
                return;
            }

            TryDispatchTasks(
                requestId,
                new CrackRequest(state.Hash, state.MaxLength),
                state);
        }
    }

    public (RequestStatus Status, List<string>? Data) GetStatus(string requestId)
    {
        if (!cache.TryGet(requestId, out var state) || state is null)
            return (RequestStatus.ERROR, null);

        lock (state)
        {
            var now = DateTime.UtcNow;

            foreach (var kv in state.InProgress)
            {
                var workerId = kv.Key;
                var task = kv.Value;

                if (task.StartedAt != null && now - task.StartedAt > WorkerTimeout)
                {
                    if (state.InProgress.TryRemove(workerId, out var failedTask))
                    {
                        failedTask.WorkerId = null;
                        failedTask.StartedAt = null;
                        state.PendingTasks.Enqueue(failedTask);
                        if (workerId >= 0 && workerId < state.WorkerAlive.Length)
                            state.WorkerAlive[workerId] = false;

                        logger.LogWarning(
                            "Request {RequestId}: [TIMEOUT] worker {WorkerId} lost {Start}-{Count}, requeue. Pending={Pending}, InProgress={InProgress}",
                            requestId,
                            workerId,
                            failedTask.Start,
                            failedTask.Count,
                            state.PendingTasks.Count,
                            state.InProgress.Count);
                    }
                }
            }

            TryDispatchTasks(
                requestId,
                new CrackRequest(state.Hash, state.MaxLength),
                state);

            if (now - state.CreatedAt > TotalTimeout)
            {
                state.Status = RequestStatus.READY;
                return (RequestStatus.READY, null);
            }

            if (state.PendingTasks.IsEmpty && state.InProgress.IsEmpty)
                state.Status = RequestStatus.READY;
            else if (state.FoundWords.Count > 0)
                state.Status = RequestStatus.PARTIAL_READY;
            else
                state.Status = RequestStatus.IN_PROGRESS;

            return (state.Status, state.FoundWords);
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

    private int? FindIdleWorker(RequestState state)
    {
        for (var i = 0; i < state.WorkerAlive.Length; i++)
        {
            if (state.WorkerAlive[i] && !state.InProgress.ContainsKey(i))
                return i;
        }

        return null;
    }

    private void EnqueueBalancedRanges(
        string requestId,
        RequestState state,
        long total,
        int workersCount,
        long startOffset)
    {
        var baseChunk = total / workersCount;
        var remainder = total % workersCount;
        var currentStart = startOffset;

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
        long total = 0;
        long power = 1;

        for (var len = 1; len <= maxLength; len++)
        {
            power *= alphabetSize;
            total += power;

            if (power < 0 || total < 0)
                return long.MaxValue;
        }

        return total;
    }
}