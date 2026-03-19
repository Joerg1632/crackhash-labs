using System.Security.Cryptography;
using System.Text;

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
    
    public Task<List<string>> CrackRangeAsync(string targetHash, int maxLength, long startIndex, long count, string alphabetStr)
    {
        var alphabet = alphabetStr.ToCharArray();
        var results = new List<string>();

        for (var i = 0; i < count; i++)
        {
            var globalIndex = startIndex + i;
            var candidate = IndexToString(globalIndex, maxLength, alphabet);

            if (string.IsNullOrEmpty(candidate))
                continue;
            
            if (ComputeMd5Hash(candidate) == targetHash)
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

    private static string ComputeMd5Hash(string s)
    {
        using (var md5Hash = MD5.Create())
        {
            var data = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(s));
            var sBuilder = new StringBuilder();
            
            foreach (var b in data)
                sBuilder.Append(b.ToString("x2"));
            
            return sBuilder.ToString();
        }
    }

    private string IndexToString(long index, int maxLength, char[] alphabet)
    {
        if (index < 0 || maxLength < 1)
            return string.Empty;

        long cumulative = 0;
        long power = 1;
        var length = 1;
        
        while (length <= maxLength)
        {
            long countAtThisLength = power * alphabet.Length;
            if (cumulative + countAtThisLength > index)
                break;

            cumulative += countAtThisLength;
            power *= alphabet.Length;
            length++;

            if (power < 0 || cumulative < 0)
                return string.Empty;
        }
        
        long localIndex = index - cumulative;
        var sb = new StringBuilder(length);

        for (int i = 0; i < length; i++)
        {
            int digit = (int)(localIndex % alphabet.Length);
            sb.Insert(0, alphabet[digit]);
            localIndex /= alphabet.Length;
        }

        return sb.ToString();
    }
}