using Manager.DTOs;
using Manager.Models;

namespace Manager.Core.Tasks;

public class TaskSplitter
{
    private readonly ILogger<TaskSplitter> logger;

    public TaskSplitter(ILogger<TaskSplitter> logger)
    {
        this.logger = logger;
    }
    
    public void EnqueueBalancedRanges(string requestId, RequestState state, long total, int workersCount)
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

    public long CalculateTotalCombinations(int alphabetSize, int maxLength)
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