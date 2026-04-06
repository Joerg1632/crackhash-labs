using Manager.DTOs;

namespace Manager.Core.Tasks;

public class TaskSplitter
{
    public List<RangeTask> GetBalancedRanges(long total, int partsCount)
    {
        var ranges = new List<RangeTask>();
        long currentStart = 0;
        var baseChunk = total / partsCount;
        var remainder = total % partsCount;

        for (var i = 0; i < partsCount; i++)
        {
            var count = baseChunk + (i < remainder ? 1 : 0);
            if (count <= 0)
                continue;

            var task = new RangeTask
            {
                Start = currentStart,
                Count = count
            };
            
            ranges.Add(task);
            currentStart += count;
        }
        
        return ranges;
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