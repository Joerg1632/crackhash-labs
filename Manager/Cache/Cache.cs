using System.Collections.Concurrent;
using Manager.DTOs;
using Manager.Enums;
using Manager.Models;

namespace Manager.Cache;

public class Cache
{
    private readonly ConcurrentDictionary<string, CachedResult> cache = new();
    private readonly int maxEntries;

    public Cache(int maxEntries = 1000)
    {
        this.maxEntries = maxEntries;
    }

    public bool TryGet(string requestId, out CachedResult? result)
    {
        return cache.TryGetValue(requestId, out result);
    }

    public bool TryGetByHashAndLength(string hash, int maxLength, out string? requestId, out CachedResult? result)
    {
        var found = cache
            .Where(kv =>
                kv.Value.Hash == hash &&
                kv.Value.MaxLength == maxLength)
            .OrderByDescending(kv => kv.Value.CreatedAt)
            .FirstOrDefault();

        if (found.Key is null)
        {
            requestId = null;
            result = null;
            return false;
        }

        requestId = found.Key;
        result = found.Value;
        return true;
    }

    public void Upsert(string requestId, CachedResult result)
    {
        cache[requestId] = result;
        EvictOldestIfNeeded();
    }

    private void EvictOldestIfNeeded()
    {
        while (cache.Count > maxEntries)
        {
            var oldest = cache
                .OrderBy(kv => kv.Value.CreatedAt)
                .FirstOrDefault();

            if (oldest.Key is null)
                return;

            cache.TryRemove(oldest.Key, out _);
        }
    }
}