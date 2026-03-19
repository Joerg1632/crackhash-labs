using System.Collections.Concurrent;
using Manager.DTOs;

namespace Manager.Services;

public class CrackManagerService
{
    private readonly IHttpClientFactory httpClientFactory;
    private readonly ConcurrentDictionary<string, RequestState> requests = new();
    private readonly string[] workerUrls;
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyz0123456789";
    
    public CrackManagerService(IHttpClientFactory factory, IConfiguration config)
    {
        httpClientFactory = factory;
        workerUrls = config.GetSection("WorkerUrls").Get<string[]>() ?? new[] { "http://worker:80" };
    }

    public string StartCrack(CrackRequest request)
    {
        var requestId = Guid.NewGuid().ToString();

        var total = CalculateTotalCombinations(Alphabet.Length, request.MaxLength);
        var baseChunk = total / workerUrls.Length;
        var remainder = total % workerUrls.Length;
        long currentStart = 0;
        var client = httpClientFactory.CreateClient();
        
        var state = new RequestState
        {
            PendingWorkers = workerUrls.Length,
            Status = "IN_PROGRESS",
            CreatedAt = DateTime.UtcNow
        };
        
        for (int i = 0; i < workerUrls.Length; i++)
        {
            state.Workers[i] = new WorkerState
            {
                StartedAt = DateTime.UtcNow,
                Completed = false
            };
        }
        
        requests[requestId] = state;
        
        for (var i = 0; i < workerUrls.Length; i++)
        {
            var count = baseChunk + (i < remainder ? 1 : 0);
            
            var task = new WorkerTaskDto(
                requestId,
                request.Hash,
                request.MaxLength,
                currentStart,
                count,
                i,
                Alphabet
            );

            _ = client.PostAsJsonAsync(
                $"{workerUrls[i]}/internal/api/worker/hash/crack/task",
                task
            );

            currentStart += count;
        }

        return requestId;
    }

    public void ReportResult(string requestId, List<string> words, int workerId)
    {
        if (!requests.TryGetValue(requestId, out var state)) 
            return;

        lock (state)
        {
            state.FoundWords.AddRange(words);

            var worker = state.Workers[workerId];
            if (!worker.Completed)
            {
                worker.Completed = true;
                state.PendingWorkers--;
            }

            if (state.PendingWorkers == 0)
                state.Status = "READY";
        }
    }

    public (string Status, List<string>? Data) GetStatus(string requestId)
    {
        if (!requests.TryGetValue(requestId, out var state))
            return ("NOT_FOUND", null);

        lock (state)
        {
            var now = DateTime.UtcNow;

            var workerTimeout = TimeSpan.FromSeconds(60);
            var totalTimeout = TimeSpan.FromMinutes(2);

            foreach (var w in state.Workers.Values)
            {
                if (!w.Completed && now - w.StartedAt > workerTimeout)
                {
                    w.Completed = true;
                    state.PendingWorkers--;
                }
            }

            if (now - state.CreatedAt > totalTimeout)
            {
                state.Status = "ERROR";
                return ("ERROR", null);
            }

            if (state.PendingWorkers == 0)
                state.Status = "READY";
            else if (state.FoundWords.Count > 0)
                state.Status = "PARTIAL_READY";

            return (state.Status, state.FoundWords);
        }
    }

    private static long CalculateTotalCombinations(int alphabetSize, int maxLength)
    {
        var total = 0;
        var power = 1;

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