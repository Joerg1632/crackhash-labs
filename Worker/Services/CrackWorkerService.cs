using Worker.Utils;
namespace Worker.Services;

public interface ICrackService
{
    Task<List<string>> CrackRangeAsync(
        string targetHash, 
        int maxLength, 
        long startIndex, 
        long count,
        string alphabet);
}

public class CrackWorkerService : ICrackService
{
    public async Task<List<string>> CrackRangeAsync(
        string targetHash,
        int maxLength,
        long startIndex,
        long count,
        string alphabetStr)
    {
        var alphabet = alphabetStr.ToCharArray();
        var threadCount = Environment.ProcessorCount;
        var chunkSize = count / threadCount;

        var tasks = Enumerable.Range(0, threadCount).Select(t =>
        {
            var chunkStart = startIndex + t * chunkSize;
            var chunkCount = t == threadCount - 1 
                ? count - t * chunkSize
                : chunkSize;

            return Task.Run(() =>
            {
                var localResults = new List<string>();

                for (var i = 0L; i < chunkCount; i++)
                {
                    var candidate = HashingHelper.IndexToString(chunkStart + i, maxLength, alphabet);
                    if (!string.IsNullOrEmpty(candidate) && HashingHelper.ComputeMd5Hash(candidate) == targetHash)
                        localResults.Add(candidate);
                }

                return localResults;
            });
        });

        var results = await Task.WhenAll(tasks);
        return results.SelectMany(x => x).ToList();
    }
}