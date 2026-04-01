using Worker.Utils;
namespace Worker.Services;

public interface ICrackService
{
    Task<List<string>> CrackRangeAsync(string targetHash, int maxLength, long startIndex, long count, string alphabet);
    Task ReportResultsAsync(string requestId, List<string> foundWords, int workerId);
}

public class CrackWorkerService : ICrackService
{
    private readonly HttpClient httpClient;
    private readonly string managerUrl;

    public CrackWorkerService(IHttpClientFactory factory, IConfiguration config)
    {
        httpClient = factory.CreateClient();
        managerUrl = config["ManagerUrl"] ?? "http://manager:8080";
    }
    
    public Task<List<string>> CrackRangeAsync(
        string targetHash, 
        int maxLength, 
        long startIndex, 
        long count, 
        string alphabetStr)
    {
        var alphabet = alphabetStr.ToCharArray();
        var results = new List<string>();

        for (var i = 0; i < count; i++)
        {
            var globalIndex = startIndex + i;
            var candidate = HashingHelper.IndexToString(globalIndex, maxLength, alphabet);

            if (string.IsNullOrEmpty(candidate))
                continue;
            
            if (HashingHelper.ComputeMd5Hash(candidate) == targetHash)
                results.Add(candidate);
        }
        
        return Task.FromResult(results);
    }

    public async Task ReportResultsAsync(string requestId, List<string> found, int workerId)
    {
        var payload = new
        {
            RequestId = requestId,
            FoundWords = found,
            WorkerId = workerId
        };

        var response = await httpClient.PatchAsJsonAsync(
            $"{managerUrl}/internal/api/manager/hash/crack/request",
            payload);

        response.EnsureSuccessStatusCode();
    }
}