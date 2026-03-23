using System.Collections.Concurrent;
using Manager.DTOs;
using Manager.Enums;

namespace Manager.Cache;

public class ResultCache
{
    private readonly ConcurrentDictionary<string, RequestState> requests = new();
    private readonly int maxEntries;

    public ResultCache(int maxEntries = 1000)
    {
        this.maxEntries = maxEntries;
    }

    public bool TryGet(string requestId, out RequestState? state)
    {
        if (requests.TryGetValue(requestId, out var existing))
        {
            state = existing;
            return true;
        }

        state = null;
        return false;
    }

    public bool TryGetByHashAndLength(string hash, int maxLength, out string? requestId, out RequestState? state)
    {
        var found = requests
            .Where(kv =>
                kv.Value.Hash == hash &&
                kv.Value.MaxLength == maxLength &&
                kv.Value.Status != RequestStatus.ERROR)
            .OrderByDescending(kv => kv.Value.CreatedAt)
            .FirstOrDefault();

        if (found.Key is null)
        {
            requestId = null;
            state = null;
            return false;
        }

        requestId = found.Key;
        state = found.Value;
        return true;
    }

    public void Upsert(string requestId, RequestState state)
    {
        requests[requestId] = state;
        EvictOldestIfNeeded();
    }

    private void EvictOldestIfNeeded()
    {
        while (requests.Count > maxEntries)
        {
            var oldest = requests
                .OrderBy(kv => kv.Value.CreatedAt)
                .FirstOrDefault();

            if (oldest.Key is null)
                return;

            requests.TryRemove(oldest.Key, out _);
        }
    }
}