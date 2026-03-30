using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

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
    private static readonly MD5 md5 = MD5.Create();

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
        lock (md5)
        {
            var data = md5.ComputeHash(Encoding.UTF8.GetBytes(s));
            var sBuilder = new StringBuilder();
            
            foreach (var b in data)
                sBuilder.Append(b.ToString("x2"));
            
            return sBuilder.ToString();
        }
    }

    private string IndexToString(long index, int maxLength, char[] alphabet)
    {
        var baseN = alphabet.Length;
        var length = 1;
        var count = baseN;

        while (index >= count && length < maxLength)
        {
            index -= count;
            length++;
            count *= baseN;
        }

        var chars = new char[length];
        for (var i = length - 1; i >= 0; i--)
        {
            chars[i] = alphabet[(int)(index % baseN)];
            index /= baseN;
        }

        return new string(chars);
    }
}